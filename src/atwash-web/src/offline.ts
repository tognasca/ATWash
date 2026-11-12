import {api,type Customer,type Vehicle,type Service,type Product,type Order} from './api';

export type Snapshot = {customers:Customer[];vehicles:Vehicle[];services:Service[];products:Product[];orders:Order[]};
type Pending =
 | {id:string;tenant:string;kind:'status';orderId:string;from:number;to:number;createdAt:number}
 | {id:string;tenant:string;kind:'customer';tempId:string;name:string;phone:string;createdAt:number}
 | {id:string;tenant:string;kind:'vehicle';tempId:string;customerId:string;plate:string;model:string;createdAt:number}
 | {id:string;tenant:string;kind:'order';tempId:string;vehicleId:string;serviceIds:string[];createdAt:number};
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
 const same=pending.filter((x):x is Extract<Pending,{kind:'status'}>=>x.tenant===tenant&&x.kind==='status'&&x.orderId===orderId).sort((a,b)=>a.createdAt-b.createdAt);
 const last=same.at(-1);
 if(last && last.to!==from)throw new Error('Etapa offline inconsistente. Sincronize antes de continuar.');
 
 const item:Pending={id:newId(),tenant,kind:'status',orderId,from,to,createdAt:Date.now()};
 await store('pending','readwrite',s=>s.add(item));
 const snapshot=await loadSnapshot(tenant);
 if(snapshot){
  const order=snapshot.orders.find(x=>x.id===orderId);
  if(order){order.status=to;await saveSnapshot(tenant,snapshot)}
 }
}
function newId():string{
 return crypto.randomUUID?.() ?? 'local-'+Date.now()+'-'+Math.random().toString(36).slice(2);
}
export async function createOfflineCustomer(tenant:string,name:string,phone:string):Promise<Customer>{
 const snapshot=await loadSnapshot(tenant);
 if(!snapshot)throw new Error('Dados locais indisponíveis.');
 const customer:Customer={id:newId(),name:name.trim(),phone:phone.trim()};
 await store('pending','readwrite',s=>s.add({id:newId(),tenant,kind:'customer',tempId:customer.id,name:customer.name,phone:customer.phone??'',createdAt:Date.now()} as Pending));
 snapshot.customers.push(customer);
 await saveSnapshot(tenant,snapshot);
 return customer;
}
export async function createOfflineVehicle(tenant:string,customerId:string,plate:string,model:string):Promise<Vehicle>{
 const snapshot=await loadSnapshot(tenant);
 if(!snapshot)throw new Error('Dados locais indisponíveis.');
 const normalized=plate.trim().toUpperCase();
 if(snapshot.vehicles.some(x=>x.plate===normalized))throw new Error('Placa já cadastrada localmente.');
 const vehicle:Vehicle={id:newId(),customerId,plate:normalized,model:model.trim()};
 await store('pending','readwrite',s=>s.add({id:newId(),tenant,kind:'vehicle',tempId:vehicle.id,customerId,plate:normalized,model:vehicle.model,createdAt:Date.now()} as Pending));
 snapshot.vehicles.push(vehicle);
 await saveSnapshot(tenant,snapshot);
 return vehicle;
}
export async function createOfflineOrder(tenant:string,vehicleId:string,serviceIds:string[]):Promise<Order>{
 const snapshot=await loadSnapshot(tenant);
 if(!snapshot)throw new Error('Dados locais indisponíveis.');
 const selected=snapshot.services.filter(x=>serviceIds.includes(x.id));
 if(selected.length!==serviceIds.length || selected.length===0)throw new Error('Serviços inválidos.');
 const order:Order={id:newId(),number:'OFF-'+Date.now(),vehicleId,total:selected.reduce((s,x)=>s+x.price,0),status:0,paymentStatus:0};
 await store('pending','readwrite',s=>s.add({id:newId(),tenant,kind:'order',tempId:order.id,vehicleId,serviceIds,createdAt:Date.now()} as Pending));
 snapshot.orders.unshift(order);
 await saveSnapshot(tenant,snapshot);
 return order;
}
export async function pendingCount(tenant:string):Promise<number>{
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 return all.filter(x=>x.tenant===tenant).length;
}
export async function syncPending(token:string):Promise<number>{
 if(!navigator.onLine)return 0;
 const tenant=tenantFromToken(token);
 let count=0;
 // Process in creation order; remap temporary IDs durably before deleting each item.
 while(true){
  const all=(await store<Pending[]>('pending','readonly',s=>s.getAll()))
   .filter(x=>x.tenant===tenant).sort((a,b)=>a.createdAt-b.createdAt ||
    ({customer:0,vehicle:1,order:2,status:3}[a.kind]-{customer:0,vehicle:1,order:2,status:3}[b.kind]));
  if(all.length===0)break;
  const item=all[0];
  if(item.kind==='status'){
   await api('/orders/'+item.orderId+'/status',token,'PATCH',{status:item.to});
  }else{
   let realId:string;
   let number:string|undefined;
   if(item.kind==='customer'){
    const result=await api<{id:string}>('/customers',token,'POST',{name:item.name,phone:item.phone});
    realId=result.id;
   }else if(item.kind==='vehicle'){
    const result=await api<{id:string}>('/vehicles',token,'POST',{customerId:item.customerId,plate:item.plate,model:item.model});
    realId=result.id;
   }else{
    const result=await api<{id:string;number:string}>('/orders',token,'POST',{vehicleId:item.vehicleId,serviceIds:item.serviceIds});
    realId=result.id;number=result.number;
   }
   const snapshot=await loadSnapshot(tenant);
   if(snapshot){
    if(item.kind==='customer'){
     const entity=snapshot.customers.find(x=>x.id===item.tempId);if(entity)entity.id=realId;
     snapshot.vehicles.forEach(x=>{if(x.customerId===item.tempId)x.customerId=realId});
    }else if(item.kind==='vehicle'){
     const entity=snapshot.vehicles.find(x=>x.id===item.tempId);if(entity)entity.id=realId;
     snapshot.orders.forEach(x=>{if(x.vehicleId===item.tempId)x.vehicleId=realId});
    }else{
     const entity=snapshot.orders.find(x=>x.id===item.tempId);if(entity){entity.id=realId;entity.number=number??entity.number}
    }
    await saveSnapshot(tenant,snapshot);
   }
   // Rewrite references in dependent pending actions before marking this action complete.
   const remaining=await store<Pending[]>('pending','readonly',s=>s.getAll());
   for(const next of remaining.filter(x=>x.tenant===tenant&&x.id!==item.id)){
    let changed=false;
    if(next.kind==='vehicle'&&next.customerId===item.tempId){next.customerId=realId;changed=true}
    if(next.kind==='order'&&next.vehicleId===item.tempId){next.vehicleId=realId;changed=true}
    if(next.kind==='status'&&next.orderId===item.tempId){next.orderId=realId;changed=true}
    if(changed)await store('pending','readwrite',s=>s.put(next));
   }
  }
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
