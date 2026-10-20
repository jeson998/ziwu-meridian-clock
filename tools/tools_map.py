import numpy as np, json, math
D=json.load(open('/tmp/mer2d.json'))
P=np.load('/tmp/mh_P2.npy'); T=np.load('/tmp/mh_T2.npy'); ARM=np.load('/tmp/mh_arm.npy'); J={k:np.array(v) for k,v in json.load(open('/tmp/mh_J2.json')).items()}
TA,TB,TC=P[T[:,0]],P[T[:,1]],P[T[:,2]]
TARM=ARM[T[:,0]]|ARM[T[:,1]]|ARM[T[:,2]]
def ray(axis,coord,sign,side=0,noarm=False,ylim=None):
    """axis-aligned ray along `axis` (0=x,1=y,2=z); coord = the other two coords (in order of remaining axes). sign=+1 -> want max along axis (ray from +inf)."""
    o=[i for i in range(3) if i!=axis]
    a,b,c=TA[:,o],TB[:,o],TC[:,o]; p=np.array(coord)
    mn=np.minimum(np.minimum(a,b),c); mx=np.maximum(np.maximum(a,b),c)
    m=(mn[:,0]<=p[0])&(mx[:,0]>=p[0])&(mn[:,1]<=p[1])&(mx[:,1]>=p[1])
    if side: m&=(TA[:,0]*side>0.05)
    if noarm: m&=~TARM
    if ylim is not None: m&=(TA[:,1]<ylim)
    sel=np.where(m)[0]
    if len(sel)==0: return None
    a,b,c=a[sel],b[sel],c[sel]
    v0=c-a; v1=b-a; v2=p-a
    d00=(v0*v0).sum(1); d01=(v0*v1).sum(1); d11=(v1*v1).sum(1); d20=(v2*v0).sum(1); d21=(v2*v1).sum(1)
    den=d00*d11-d01*d01; den[den==0]=1e-12
    u=(d11*d20-d01*d21)/den; v=(d00*d21-d01*d20)/den
    ok=(u>=-1e-6)&(v>=-1e-6)&(u+v<=1+1e-6)
    if not ok.any(): return None
    za,zb,zc=TA[sel,axis],TB[sel,axis],TC[sel,axis]
    depth=za+u*(zc-za)+v*(zb-za)
    depth=depth[ok]
    return depth.max() if sign>0 else depth.min()

# ---- 2D helpers (same as meridians.js)
AC=np.array(D['AC'],float); LC=np.array(D['LC'],float)
def wAt(tab,t):
    for i in range(len(tab)-1):
        if t<=tab[i+1][0]:
            f=(t-tab[i][0])/(tab[i+1][0]-tab[i][0]); return tab[i][1]+(tab[i+1][1]-tab[i][1])*f
    return tab[-1][1]
def chain_param(C,W,x,y,tmax):
    best=None
    for i in range(len(C)-1):
        a,b=C[i],C[i+1]; ab=b-a; L2=ab@ab
        f=np.clip(((np.array([x,y])-a)@ab)/L2,0,1); q=a+f*ab
        d=np.hypot(x-q[0],y-q[1])
        nrm=np.array([ab[1],-ab[0]])/math.sqrt(L2)
        sd=(np.array([x,y])-q)@nrm
        if best is None or d<best[0]: best=(d,i+f,sd)
    t=min(best[1],tmax); return t, best[2]/wAt(W,t)
# model chains
def mchain(names): return np.array([J[n] for n in names])
MA=mchain(['l-shoulder','l-elbow','l-hand','l-finger-3-4'])
ML=mchain(['l-upper-leg','l-knee','l-ankle'])
s_scale=(P[:,1].max())/876.0   # model units per 2D unit
def chain_point(Mc,t,u,w2d,depth_sign):
    i=min(int(t),len(Mc)-2); f=t-i; c=Mc[i]+(Mc[i+1]-Mc[i])*f
    d=Mc[i+1]-Mc[i]; d2=np.array([d[0],d[1]]); d2/=np.linalg.norm(d2)
    lat=np.array([-d2[1],d2[0]])            # perpendicular in frontal plane
    if lat[0]<0: lat=-lat                    # point laterally (+x)
    w=w2d*s_scale
    for k in range(12):
        X,Y=c[0]+lat[0]*u*w, c[1]+lat[1]*u*w
        z=ray(2,(X,Y),depth_sign)
        if z is not None: return np.array([X,Y,z])
        u*=0.85
    return np.array([c[0],c[1],c[2]])
# vertical landmark map 2D y -> model Y
Ytop=P[:,1].max()
head=P[(P[:,1]>J['neck'][1])&(~ARM)]
front=head[np.abs(head[:,0])<0.15]
Ynose=front[np.argmax(front[:,2]),1]
cand=front[(front[:,1]<Ynose-0.3)]
# chin: lowest point of the face front (max z region under mouth) – take local: min y with z>J['jaw'][2]-0.25
chin=cand[cand[:,2]>J['jaw'][2]-0.35]; Ychin=chin[:,1].min()
LM=[(4,Ytop),(56,J['l-eye'][1]),(84,Ynose),(112,Ychin),(152,J['l-clavicle'][1]),(178,J['l-shoulder'][1]),(470,J['l-upper-leg'][1]),(650,J['l-knee'][1]),(830,J['l-ankle'][1]),(880,0.0)]
def mapY(y):
    for i in range(len(LM)-1):
        if y<=LM[i+1][0]:
            a,b=LM[i],LM[i+1]; f=(y-a[0])/(b[0]-a[0]); return a[1]+(b[1]-a[1])*f
    return 0.0
headHalf=np.abs(head[(head[:,1]>J['l-eye'][1]-0.3)&(head[:,1]<J['l-eye'][1]+0.3)][:,0]).max()
TORSO=[[132,22,20],[146,40,24],[156,78,30],[172,96,34],[200,86,38],[250,78,40],[300,74,37],[360,66,32],[400,72,34],[440,86,38],[470,90,40],[492,80,38]]
def tW(y):
    for i in range(len(TORSO)-1):
        if y<=TORSO[i+1][0]:
            f=(y-TORSO[i][0])/(TORSO[i+1][0]-TORSO[i][0]); return TORSO[i][1]+(TORSO[i+1][1]-TORSO[i][1])*f
    return TORSO[-1][1]
NA=P[~ARM]
def torsoHalf(Y):
    s=NA[np.abs(NA[:,1]-Y)<0.06]; s=s[np.abs(s[:,0])<2.6]
    return np.abs(s[:,0]).max() if len(s) else 1.0
FINGER={'LU11':('l-finger-1-4',+1),'LI1':('l-finger-2-4',+1),'PC9':('l-finger-3-4',0),'TE1':('l-finger-4-4',-1),'HT9':('l-finger-5-4',+1),'SI1':('l-finger-5-4',-1)}
footC=(J['l-ankle']+J['l-foot-1'])/2
toeZ=P[(P[:,1]<0.6)&(P[:,0]>0)][:,2].max(); heelZ=P[(P[:,1]<0.6)&(P[:,0]>0)][:,2].min()
def torso_hit(X,Y,sg,sx):
    half=torsoHalf(Y)
    if abs(X)>0.9*half:
        sl=NA[(np.abs(NA[:,1]-Y)<0.08)&(NA[:,0]*sx>0.05)]
        zc=(sl[:,2].mean() if len(sl) else 0)+sg*0.2
        xx=ray(0,(Y,zc),sx,sx,True)
        if xx is not None: return np.array([xx,Y,zc])
    for dy in np.arange(0,0.9,0.05):
        for sh in (1,0.93,0.86):
            z=ray(2,(X*sh,Y-dy),sg,0,True)
            if z is not None: return np.array([X*sh,Y-dy,z])
    return np.array([X,Y,0])
def is_arm(ax,y):
    if y<186: return False
    t,u=chain_param(AC,D['AW'],ax,y,3.0)
    return abs(u)<=1.35 and (y>500 or ax>tW(min(y,492))-2)
def map_pt(x,y,v,code=None):
    sg=1 if v=='F' else -1; ax=abs(x); sx=1 if x>=0 else -1
    if code in FINGER:
        jn,off=FINGER[code]; p=J[jn].copy(); p[0]=p[0]
        p=p+np.array([0.06*off,0,0.08*sg]); p[0]*=sx; return p
    if y>842 or code=='KI1':
        f=min(1,max(0,(y-838)/42))
        if code=='KI1':
            Z=heelZ+(toeZ-heelZ)*0.62; X=footC[0]
            yy=ray(1,(Z,X),-1) if False else None
            # ray along y from below: coords for axis 1 are (x,z)
            yb=ray(1,(X,Z),-1,0,False,1.6); return np.array([X*sx,(yb if yb is not None else 0.02)-0.01,Z])
        if v=='B':   # lateral foot edge (BL62/64/67)
            Z=heelZ+(toeZ-heelZ)*(0.25+0.7*f); Yf=0.32-0.15*f
            xx=ray(0,(Yf,Z),+1,1,False,1.6); return np.array([(xx if xx is not None else footC[0]+0.4)*sx,Yf,Z])
        Z=J['l-ankle'][2]+0.25+(toeZ-0.12-J['l-ankle'][2]-0.25)*f
        if ax<=27:   # medial edge
            Yf=0.35-0.12*f; xx=ray(0,(Yf,Z),-1,1,False,1.6)
            return np.array([(xx if xx is not None else footC[0]-0.4)*sx,Yf,Z])
        X=footC[0]+(ax-44)*s_scale*0.95
        yy=ray(1,(X,Z),+1,0,False,1.6); return np.array([X*sx,(yy if yy is not None else 0.4),Z])
    if y<112 and ax<48:
        X=x/40.0*headHalf; Y=mapY(y)
        z=ray(2,(X,Y),sg); 
        if z is None: z=J['head'][2]
        return np.array([X,Y,z])
    if not is_arm(ax,y) and y<=486:
        if True:
            Y=mapY(y); half=torsoHalf(Y); X=(x/tW(y)*half*0.97 if y>=200 else x*s_scale*1.02) if y>=132 else x/22.0*half
            return torso_hit(X,Y,sg,sx)
    if is_arm(ax,y):
        t,u=chain_param(AC,D['AW'],ax,y,3.0)
        p=chain_point(MA,t,u,wAt(D['AW'],t),sg); p[0]*=sx; return p
    t,u=chain_param(LC,D['LW'],ax,y,2.0)
    p=chain_point(ML,t,u,wAt(D['LW'],t),sg); p[0]*=sx; return p
def cr2D(pts,per=6):
    out=[]
    for i in range(len(pts)-1):
        p0=pts[i-1] if i>0 else pts[i]; p1=pts[i]; p2=pts[i+1]; p3=pts[i+2] if i+2<len(pts) else p2
        n=max(2,int(math.ceil(math.hypot(p2[0]-p1[0],p2[1]-p1[1])/per)))
        for k in range(n):
            t=k/n; t2=t*t; t3=t2*t
            f=lambda a,b,c,d:.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3)
            out.append((f(p0[0],p1[0],p2[0],p3[0]),f(p0[1],p1[1],p2[1],p3[1]),i,t))
    out.append((pts[-1][0],pts[-1][1],len(pts)-2,1.0))
    return out
res={}
for k,m in D['M'].items():
    segs=[]
    for sg in m['segs']:
        for side in (1,-1):
            pts=[(p[0]*side,p[1],p[2]) for p in sg['p']]
            anc=[map_pt(x,y,sg['v'],c) for x,y,c in pts]
            gen=[map_pt(x,y,sg['v'],None) if c in FINGER else a for (x,y,c),a in zip(pts,anc)]
            err=[a-g for a,g in zip(anc,gen)]
            out=[]
            for (x,y,i,t) in cr2D([(p[0],p[1]) for p in pts]):
                q=map_pt(x,y,sg['v'],None)+err[i]*(1-t)+err[min(i+1,len(err)-1)]*t
                out.append([round(float(c),4) for c in q])
            segs.append({'v':sg['v'],'side':side,'pts':out,'anchors':[{'code':c,'p':[round(float(v),4) for v in a]} for (x,y,c),a in zip(pts,anc) if c]})
    res[k]={'segs':segs}
    print(k,len(segs),sum(len(s['pts']) for s in segs))

# outward offset along nearest vertex normal
fn=np.cross(TB-TA,TC-TA); VN=np.zeros_like(P)
for i in range(3): np.add.at(VN,T[:,i],fn)
VN/=np.linalg.norm(VN,axis=1,keepdims=True)+1e-9
class KD:
    def query(self,a,k=6):
        a=np.atleast_2d(a); out=[]
        for i in range(0,len(a),256):
            d=((a[i:i+256,None,:]-P[None])**2).sum(-1); out.append(np.argsort(d,axis=1)[:,:k])
        r=np.concatenate(out); return None,(r if r.shape[0]>1 else r[0])
kd=KD()
for k in res:
    for sg in res[k]['segs']:
        a=np.array(sg['pts']); _,ii=kd.query(a,k=6); n=VN[ii].mean(1); n/=np.linalg.norm(n,axis=1,keepdims=True)+1e-9
        sg['pts']=(a+n*0.03).round(3).tolist()
        for an in sg['anchors']:
            p=np.array(an['p']); _,ii=kd.query(p,k=6); n=VN[ii].mean(0); n/=np.linalg.norm(n); an['p']=(p+n*0.05).round(3).tolist()
json.dump(res,open('models/meridians3d.json','w'))
