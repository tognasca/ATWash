import {useEffect,useState,type FormEvent} from 'react';
import {createRoot} from 'react-dom/client';
import {api,ApiError,type Customer,type Vehicle,type Service,type Product,type Order} from './api';
import Atendimento from './Atendimento';
import Admin from './Admin';
import {saveSnapshot,loadSnapshot,syncPending,tenantFromToken,pendingCount} from './offline';
import {DialogProvider,useDialog} from './Dialog';
import './style.css';

function App(){
 const dialog=useDialog();
 const notifyError=(message:string)=>{if(message.trim())dialog.show(message,'error')};
 const [token,setToken]=useState('');
 const [deviceId]=useState(()=>{const key='atwash-device-id';let id=localStorage.getItem(key);if(!id){id=crypto.randomUUID();localStorage.setItem(key,id)}return id});
 const [role,setRole]=useState('');
 const [screen,setScreen]=useState<'operation'|'admin'>('operation');
 const [email,setEmail]=useState('');
 const [password,setPassword]=useState('');
 const [error,setError]=useState('');
 const [busy,setBusy]=useState(false);
 const [online,setOnline]=useState(navigator.onLine);
 const [queued,setQueued]=useState(0);
 const [syncing,setSyncing]=useState(false);
 useEffect(()=>{
  const onOnline=()=>{setOnline(true);if(token)void syncPending(token).then(()=>reload(token)).catch(e=>notifyError('Sincronização pendente: '+String(e)))};
  const onOffline=()=>setOnline(false);
  window.addEventListener('online',onOnline);window.addEventListener('offline',onOffline);
  return ()=>{window.removeEventListener('online',onOnline);window.removeEventListener('offline',onOffline)};
 },[token]);
 const [customers,setCustomers]=useState<Customer[]>([]);
 const [vehicles,setVehicles]=useState<Vehicle[]>([]);
 const [services,setServices]=useState<Service[]>([]);
 const [products,setProducts]=useState<Product[]>([]);
 const [orders,setOrders]=useState<Order[]>([]);
 async function reload(t=token){
  const tenant=tenantFromToken(t);
  try{
   const [c,v,s,p,o]=await Promise.all([
    api<Customer[]>('/customers',t),api<Vehicle[]>('/vehicles',t),
    api<Service[]>('/services',t),api<Product[]>('/products',t),api<Order[]>('/orders',t)
   ]);
   setCustomers(c);setVehicles(v);setServices(s);setProducts(p);setOrders(o);
   await saveSnapshot(tenant,{customers:c,vehicles:v,services:s,products:p,orders:o});
  }catch(e){
   if(navigator.onLine)throw e;
   const cached=await loadSnapshot(tenant);
   if(!cached)throw new Error('Sem dados offline. Conecte-se e entre ao menos uma vez.');
   setCustomers(cached.customers);setVehicles(cached.vehicles);setServices(cached.services);
   setProducts(cached.products);setOrders(cached.orders);
  }
  setQueued(await pendingCount(tenant));
 }
 async function login(e:FormEvent){
  e.preventDefault();setBusy(true);setError('');
  try{
   const result=await api<{accessToken:string}>('/auth/login','','POST',{email,password,deviceId,deviceName:navigator.userAgent.slice(0,100)});
   const me=await api<{role:string}>('/auth/me',result.accessToken);
   await reload(result.accessToken);
   setRole(me.role);setScreen('operation');setToken(result.accessToken);setPassword('');
  }catch(e){if(e instanceof ApiError && e.status===401)dialog.show('E-mail ou senha incorretos. Verifique os dados e tente novamente.','error');else dialog.show(e instanceof ApiError&&e.status===429?'Muitas tentativas de acesso. Aguarde um minuto e tente novamente.':'Não foi possível entrar. Verifique a conexão e tente novamente.','error')}finally{setBusy(false)}
 }
 if(!token)return <main className="login"><section className="panel"><div className="brand"><img className="brand-logo" src="/atwashcontrol-logo.png" alt="ATWashControl — Gestão de Lava-Rápido" onError={e=>{e.currentTarget.style.display="none";e.currentTarget.nextElementSibling?.removeAttribute("hidden")}}/><span className="brand-fallback" hidden>ATWash<span>Control</span></span></div><p>Seu lava-rápido na palma da mão.</p><form onSubmit={login}><label>E-mail<input type="email" required value={email} onChange={e=>setEmail(e.target.value)}/></label><label>Senha<input type="password" required value={password} onChange={e=>setPassword(e.target.value)}/></label><button disabled={busy}>Entrar</button></form></section></main>;
 const isAdmin=role==='Owner'||role==='Manager';
 return <main><header><h1>AT<span>Wash</span></h1><div><button className={screen==='operation'?'':'secondary'} onClick={()=>setScreen('operation')}>Atendimento</button>{isAdmin&&<button className={screen==='admin'?'':'secondary'} onClick={()=>setScreen('admin')}>Administração</button>}<button className="secondary" onClick={()=>{setToken('');setRole('');setError('');setScreen('operation')}}>Sair</button></div></header>
 {error&&<p role="alert" className="error">{error}</p>}
 {screen==='operation'?<Atendimento token={token} customers={customers} vehicles={vehicles} services={services} products={products} orders={orders} reload={reload} onError={notifyError} canConfirmPix={isAdmin} canWrite={role!=='Washer'} online={online} onQueued={()=>setQueued(x=>x+1)}/>:isAdmin?<Admin token={token} services={services} products={products} reload={reload} onError={setError}/>:null}
 <footer><button className="secondary" disabled={!online||syncing||queued===0} onClick={()=>{setSyncing(true);void syncPending(token).then(()=>reload(token)).catch(e=>notifyError('Conflito de sincronização: '+String(e))).finally(()=>setSyncing(false))}}>Sincronizar pendências ({queued})</button> ATWash · {online?'Online':'Offline — consulta local e etapas de lavagem'} · {queued} etapa(s) pendente(s) · Em homologação</footer></main>;
}
createRoot(document.getElementById('root')!).render(<DialogProvider><App/></DialogProvider>);
