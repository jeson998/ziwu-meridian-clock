import numpy as np, collections, json, sys
V=[];groups=collections.defaultdict(list);cur=None
for line in open('models/base.obj'):
    if line.startswith('v '): V.append([float(x) for x in line.split()[1:4]])
    elif line.startswith('g '): cur=line.split()[1]
    elif line.startswith('f '): groups[cur].append([int(t.split('/')[0])-1 for t in line.split()[1:]])
V=np.array(V)
J={g[6:]:V[sorted(set(i for f in fs for i in f))].mean(0) for g,fs in groups.items() if g.startswith('joint-')}
body=groups['body']; vi=sorted(set(i for f in body for i in f)); remap={o:n for n,o in enumerate(vi)}
P=V[vi].copy()
tris=[]
for f in body:
    f=[remap[i] for i in f]
    for k in range(1,len(f)-1): tris.append([f[0],f[k],f[k+1]])
tris=np.array(tris,dtype=np.uint32)
np.save('/tmp/mh_P.npy',P); np.save('/tmp/mh_T.npy',tris); json.dump({k:v.tolist() for k,v in J.items()},open('/tmp/mh_J.json','w'))
def write(P,tris,J,name):
    P=P.astype(np.float32)
    open(f'models/{name}.bin','wb').write(P.tobytes()+tris.astype(np.uint32).tobytes())
    json.dump({'nv':len(P),'nt':len(tris),'joints':{k:list(map(float,v)) for k,v in J.items()}},open(f'models/{name}.json','w'))
if __name__=='__main__': write(P,tris,J,'mh_raw'); print(len(P),len(tris))
