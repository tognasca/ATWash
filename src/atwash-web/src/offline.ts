import {api,type Customer,type Vehicle,type Service,type Product,type Order} from './api';

export type Snapshot = {customers:Customer[];vehicles:Vehicle[];services:Service[];products:Product[];orders:Order[]};
type Pending = {id:string;tenant:string;orderId:string;from:number;to:number;createdAt:number};
const DB_NAME='atwash-offline-v1';
function open():Promise<IDBDatabase>{
 return new Promise((resolve,reject)=>{
  const req=indexedDB.open(DB_NAME,1);
  req.onupgradeneeded=()=>{
   const db=req.result;
   db.createObjectStore('snapshots');
   db.createObjectStore('pending',{keyPath:'id'});
  };
  req.onsuccess=()=>resolve(req.result);
  req.onerror=()=>reject(req.error);
 });
}
async function store<T>(name:string,mode:IDBTransactionMode,fn:(s:IDBObjectStore)=>IDBRequest<T>):Promise<T>{
 const db=await open();
 return new Promise((resolve,reject)=>{
  const tx=db.transaction(name,mode);
  const request=fn(tx.objectStore(name));
  request.onsuccess=()=>resolve(request.result);
  request.onerror=()=>reject(request.error);
  tx.oncomplete=()=>db.close();
  tx.onerror=()=>{db.close();reject(tx.error)};
 });
}
export function tenantFromToken(token:string):string{
 try{
  const part=token.split('.')[1];
  const decoded=JSON.parse(atob(part.replace(/-/g,'+').replace(/_/g,'/')));
  if(typeof decoded.tenant_id==='string' && /^[0-9a-f-]{36}$/i.test(decoded.tenant_id))return decoded.tenant_id;
 }catch{/* invalid token */}
 throw new Error('Sessão inválida. Faça login novamente.');
}
export async function saveSnapshot(tenant:string,snapshot:Snapshot){
 await store('snapshots','readwrite',s=>s.put(snapshot,tenant));
}
export async function loadSnapshot(tenant:string):Promise<Snapshot|undefined>{
 return store('snapshots','readonly',s=>s.get(tenant));
}
export async function queueStatus(tenant:string,orderId:string,from:number,to:number){
 if(to!==from+1 || to>3)throw new Error('Apenas avanço de etapas de lavagem pode ser enfileirado offline.');
 const pending=await store<Pending[]>('pending','readonly',s=>s.getAll());
 const same=pending.filter(x=>x.tenant===tenant&&x.orderId===orderId).sort((a,b)=>a.createdAt-b.createdAt);
 const last=same.at(-1);
 if(last && last.to!==from)throw new Error('Etapa offline inconsistente. Sincronize antes de continuar.');
 if(!last && pending.some(x=>x.tenant===tenant&&x.orderId===orderId))throw new Error('Sincronize a comanda.');
 const item:Pending={id:crypto.randomUUID?.() ?? String(Date.now())+'-'+Math.random(),tenant,orderId,from,to,createdAt:Date.now()};
 await store('pending','readwrite',s=>s.add(item));
}
export async function pendingCount(tenant:string):Promise<number>{
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 return all.filter(x=>x.tenant===tenant).length;
}
export async function syncStatuses(token:string):Promise<number>{
 if(!navigator.onLine)return 0;
 const tenant=tenantFromToken(token);
 const all=(await store<Pending[]>('pending','readonly',s=>s.getAll()))
  .filter(x=>x.tenant===tenant).sort((a,b)=>a.createdAt-b.createdAt);
 let count=0;
 for(const item of all){
  // Never silently drop conflicts: preserve queued changes for manual review.
  await api('/orders/'+item.orderId+'/status',token,'PATCH',{status:item.to});
  await store('pending','readwrite',s=>s.delete(item.id));
  count++;
 }
 return count;
}
export async function clearOfflineTenant(tenant:string){
 await store('snapshots','readwrite',s=>s.delete(tenant));
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 for(const item of all.filter(x=>x.tenant===tenant))
  await store('pending','readwrite',s=>s.delete(item.id));
}
