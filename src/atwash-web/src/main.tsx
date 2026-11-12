import {useState,type FormEvent} from 'react';
import {createRoot} from 'react-dom/client';
import {api,type Customer,type Vehicle,type Service,type Product,type Order} from './api';
import Atendimento from './Atendimento';
import Admin from './Admin';
import './style.css';

function App(){
 const [token,setToken]=useState('');
 const [role,setRole]=useState('');
 const [screen,setScreen]=useState<'operation'|'admin'>('operation');
 const [email,setEmail]=useState('');
 const [password,setPassword]=useState('');
 const [error,setError]=useState('');
 const [busy,setBusy]=useState(false);
 const [customers,setCustomers]=useState<Customer[]>([]);
 const [vehicles,setVehicles]=useState<Vehicle[]>([]);
 const [services,setServices]=useState<Service[]>([]);
 const [products,setProducts]=useState<Product[]>([]);
 const [orders,setOrders]=useState<Order[]>([]);
 async function reload(t=token){
  const [c,v,s,p,o]=await Promise.all([
   api<Customer[]>('/customers',t),api<Vehicle[]>('/vehicles',t),
   api<Service[]>('/services',t),api<Product[]>('/products',t),api<Order[]>('/orders',t)
  ]);
  setCustomers(c);setVehicles(v);setServices(s);setProducts(p);setOrders(o);
 }
 async function login(e:FormEvent){
  e.preventDefault();setBusy(true);setError('');
  try{
   const result=await api<{accessToken:string}>('/auth/login','','POST',{email,password});
   const me=await api<{role:string}>('/auth/me',result.accessToken);
   await reload(result.accessToken);
   setRole(me.role);setScreen('operation');setToken(result.accessToken);setPassword('');
  }catch(e){setError(String(e))}finally{setBusy(false)}
 }
 if(!token)return <main className="login"><section className="panel"><h1>AT<span>Wash</span></h1><p>Seu lava-rápido na palma da mão.</p><form onSubmit={login}><label>E-mail<input type="email" required value={email} onChange={e=>setEmail(e.target.value)}/></label><label>Senha<input type="password" required value={password} onChange={e=>setPassword(e.target.value)}/></label><button disabled={busy}>Entrar</button></form>{error&&<p role="alert" className="error">{error}</p>}</section></main>;
 const isAdmin=role==='Owner'||role==='Manager';
 return <main><header><h1>AT<span>Wash</span></h1><div><button className={screen==='operation'?'':'secondary'} onClick={()=>setScreen('operation')}>Atendimento</button>{isAdmin&&<button className={screen==='admin'?'':'secondary'} onClick={()=>setScreen('admin')}>Administração</button>}<button className="secondary" onClick={()=>{setToken('');setRole('');setError('');setScreen('operation')}}>Sair</button></div></header>
 {error&&<p role="alert" className="error">{error}</p>}
 {screen==='operation'?<Atendimento token={token} customers={customers} vehicles={vehicles} services={services} orders={orders} reload={reload} onError={setError} canConfirmPix={isAdmin}/>:isAdmin?<Admin token={token} services={services} products={products} reload={reload} onError={setError}/>:null}
 <footer>ATWash · Em desenvolvimento · Não homologado para produção</footer></main>;
}
createRoot(document.getElementById('root')!).render(<App/>);
