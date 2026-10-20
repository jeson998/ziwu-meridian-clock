import numpy as np, collections, json, math
V=[];groups=collections.defaultdict(list);cur=None
for line in open('models/base.obj'):
    if line.startswith('v '): V.append([float(x) for x in line.split()[1:4]])
    elif line.startswith('g '): cur=line.split()[1]
    elif line.startswith('f '): groups[cur].append([int(t.split('/')[0])-1 for t in line.split()[1:]])
V=np.array(V)
def apply(fn,w):
    for line in open(fn):
        if line.startswith('#') or not line.strip(): continue
        a=line.split(); V[int(a[0])]+=w*np.array([float(a[1]),float(a[2]),float(a[3])])
apply('models/asian-male-young.target',1.0)
apply('models/universal-male-young-maxmuscle-averageweight.target',0.45)
J={g[6:]:V[sorted(set(i for f in fs for i in f))].mean(0) for g,fs in groups.items() if g.startswith('joint-')}
body=groups['body']; vi=sorted(set(i for f in body for i in f)); remap={o:n for n,o in enumerate(vi)}
P=V[vi].copy()
tris=np.array([[remap[f[0]],remap[f[k]],remap[f[k+1]]] for f in body for k in range(1,len(f)-1)],dtype=np.uint32)

def rot(axis,ang):
    axis=axis/np.linalg.norm(axis); a=math.cos(ang/2); b,c,d=-axis*math.sin(ang/2)
    return np.array([[a*a+b*b-c*c-d*d,2*(b*c+a*d),2*(b*d-a*c)],[2*(b*c-a*d),a*a+c*c-b*b-d*d,2*(c*d+a*b)],[2*(b*d+a*c),2*(c*d-a*b),a*a+d*d-b*b-c*c]])
def smooth(x): x=np.clip(x,0,1); return x*x*(3-2*x)
JJ={k:v.copy() for k,v in J.items()}
ARM=np.zeros(len(P),bool)
for side,sx in (('l',1),('r',-1)):
    S=JJ[f'{side}-shoulder'].copy(); E=JJ[f'{side}-elbow'].copy(); H=JJ[f'{side}-hand'].copy()
    F3=JJ[f'{side}-finger-3-4'].copy()
    def segd(A,B):
        ab=B-A; tt=np.clip(((P-A)@ab)/(ab@ab),0,1); return np.linalg.norm(P-(A+tt[:,None]*ab),axis=1)
    F1=JJ[f'{side}-finger-1-4'].copy(); F5=JJ[f'{side}-finger-5-4'].copy()
    darm=np.minimum(segd(S,E),segd(E,H))
    dhand=np.minimum(np.minimum(segd(H,F3),segd(H,F1)),np.minimum(segd(H,F5),np.linalg.norm(P-H,axis=1)))
    m=((P[:,0]*sx)>0.7)&(P[:,1]>0.3)&((darm<0.8)|(dhand<0.9))
    print(side,'arm verts',m.sum()); ARM|=m
    # weight along shoulder->elbow axis for shoulder rotation
    ax=E-S; L=np.linalg.norm(ax); t=((P-S)@ax)/L/L
    # vertices belonging to arm: lateral of a plane near shoulder
    w_sh=smooth((t+0.18)/0.36)*m
    # 1) straighten elbow: rotate forearm so it is colinear with upper arm (remove forward bend)
    fa=H-E; tf=((P-E)@(fa/np.linalg.norm(fa)))/np.linalg.norm(fa)
    w_el=smooth((tf+0.12)/0.24)*(t>0.5)*m
    u1=ax/L; u2=fa/np.linalg.norm(fa)
    cr=np.cross(u2,u1); ang=math.asin(min(1,np.linalg.norm(cr)))
    R=rot(cr,ang)
    Pn=P.copy(); moved=(P-E)@R.T+E
    Pn=P+(moved-P)*w_el[:,None]
    Hn=(H-E)@R.T+E
    J_upd={}
    for k,v in JJ.items():
        if k.startswith(f'{side}-hand') or k.startswith(f'{side}-finger'): JJ[k]=(v-E)@R.T+E
    P=Pn
    # 2) supinate forearm/hand: palm forward. rotate about forearm axis
    axis=u1; sup=sx*math.radians(float(__import__('os').environ.get('SUP','-100')))
    R2=rot(axis,sup)
    tf2=((P-E)@u1)/np.linalg.norm(Hn-E)
    w_sp=smooth(tf2/1.0)*(t>0.55)*m
    moved=(P-E)@R2.T+E; P=P+(moved-P)*w_sp[:,None]
    for k,v in JJ.items():
        if k.startswith(f'{side}-hand') or k.startswith(f'{side}-finger'): JJ[k]=(v-E)@R2.T+E
    # 3) lower whole arm around shoulder in frontal plane (and slightly back) to ~12deg from vertical
    cur_ang=math.atan2(-(E-S)[1],(E-S)[0]*sx)   # angle below horizontal
    target=math.radians(float(__import__('os').environ.get('ARM','76')))
    R3=rot(np.array([0,0,1.0]),-sx*(target-cur_ang))
    moved=(P-S)@R3.T+S; P=P+(moved-P)*w_sh[:,None]
    for k,v in JJ.items():
        if k in (f'{side}-elbow',) or k.startswith(f'{side}-hand') or k.startswith(f'{side}-finger'): JJ[k]=(v-S)@R3.T+S
# normalize: feet on ground y=0, height -> 1.0 * H
ymin=P[:,1].min(); P[:,1]-=ymin
for k in JJ: JJ[k]=JJ[k]-np.array([0,ymin,0])
np.save('/tmp/mh_arm.npy',ARM); np.save('/tmp/mh_P2.npy',P); np.save('/tmp/mh_T2.npy',tris); json.dump({k:list(map(float,v)) for k,v in JJ.items()},open('/tmp/mh_J2.json','w'))
open('models/body.bin','wb').write(P.astype(np.float32).tobytes()+tris.tobytes())
json.dump({'nv':len(P),'nt':len(tris),'joints':{k:list(map(float,v)) for k,v in JJ.items()}},open('models/body.json','w'))
print('height',P[:,1].max(), 'width',P[:,0].min(),P[:,0].max())
