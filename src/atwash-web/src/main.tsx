import React, {useEffect, useState} from 'react';
import {createRoot} from 'react-dom/client';
import './style.css';

type Customer={id:string;name:string;phone?:string};
type Vehicle={id:string;plate:string;model:string;brand?:string};
type Service={id:string;name:string;price:number};
type Order={id:string;number:string;status:number;paymentStatus:number;total:number};
const currency=(n:number)=>new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(n);
async function api<T>(path:string,token:string,method='GET',body?:unknown):Promise<T>{
 const response=await fetch('/api'+path,{method,headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined});
 if(!response.ok)throw new Error('Falha '+response.status+': '+(await response.text()).slice(0,200));
 return response.json();
}
function App(){
 const [token,setToken]=useState('');
 const [email,setEmail]=useState('');
 const [password,setPassword]=useState('');
 const [error,setError]=useState('');
 const [busy,setBusy]=useState(false);
 const [customers,setCustomers]=useState<Customer[]>([]);
 const [vehicles,setVehicles]=useState<Vehicle[]>([]);
 const [services,setServices]=useState<Service[]>([]);
 const [orders,setOrders]=useState<Order[]>([]);
 const [name,setName]=useState('');
 const [phone,setPhone]=useState('');
 const [customerId,setCustomerId]=useState('');
 const [plate,setPlate]=useState('');
 const [model,setModel]=useState('');
 const [vehicleId,setVehicleId]=useState('');
 const [serviceId,setServiceId]=useState('');
 const [number,setNumber]=useState('');
 const [serviceName,setServiceName]=useState('');
 const [servicePrice,setServicePrice]=useState('');
 const [editingService,setEditingService]=useState<string|null>(null);
 async function refresh(t=token){
  const [c,v,s,o]=await Promise.all([api<Customer[]>('/customers',t),api<Vehicle[]>('/vehicles',t),api<Service[]>('/services',t),api<Order[]>('/orders',t)]);
  setCustomers(c);setVehicles(v);setServices(s);setOrders(o);
 }
 async function login(e:React.FormEvent){
  e.preventDefault();setBusy(true);setError('');
  try{
   const result=await api<{accessToken:string}>('/auth/login','', 'POST',{email,password});
   setToken(result.accessToken);setPassword('');await refresh(result.accessToken);
  }catch(e){setError(String(e));}finally{setBusy(false);}
 }
 async function submit(action:()=>Promise<unknown>){
  setBusy(true);setError('');try{await action();await refresh();}catch(e){setError(String(e));}finally{setBusy(false);}
 }
 if(!token)return <main className="login"><section className="panel"><h1>AT<span>Wash</span></h1><p>Seu lava-rápido na palma da mão.</p><form onSubmit={login}><label>E-mail<input type="email" required value={email} onChange={e=>setEmail(e.target.value)}/></label><label>Senha<input type="password" required value={password} onChange={e=>setPassword(e.target.value)}/></label><button disabled={busy}>Entrar</button></form>{error&&<p role="alert" className="error">{error}</p>}<small>É necessário provisionar uma conta no backend antes do primeiro login.</small></section></main>;
 return <main><header><h1>AT<span>Wash</span></h1><div><span>Operação</span><button className="secondary" onClick={()=>{setToken('');setCustomers([]);setOrders([]);}}>Sair</button></div></header><section className="stats"><article><small>Comandas</small><strong>{orders.length}</strong></article><article><small>Em andamento</small><strong>{orders.filter(o=>o.status===1||o.status===2).length}</strong></article><article><small>Total das comandas exibidas</small><strong>{currency(orders.reduce((sum,o)=>sum+o.total,0))}</strong></article></section>{error&&<p role="alert" className="error">{error}</p>}
 <div className="columns"><section className="panel"><h2>Novo cliente</h2><form onSubmit={e=>{e.preventDefault();void submit(async()=>{await api('/customers',token,'POST',{name,phone});setName('');setPhone('');});}}><input placeholder="Nome" required value={name} onChange={e=>setName(e.target.value)}/><input placeholder="Telefone" value={phone} onChange={e=>setPhone(e.target.value)}/><button disabled={busy}>Cadastrar cliente</button></form></section>
 <section className="panel"><h2>Novo veículo</h2><form onSubmit={e=>{e.preventDefault();void submit(async()=>{await api('/vehicles',token,'POST',{customerId,plate,model});setPlate('');setModel('');});}}><select required value={customerId} onChange={e=>setCustomerId(e.target.value)}><option value="">Selecione o cliente</option>{customers.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select><input required placeholder="Placa" value={plate} onChange={e=>setPlate(e.target.value)}/><input required placeholder="Modelo" value={model} onChange={e=>setModel(e.target.value)}/><button disabled={busy}>Cadastrar veículo</button></form></section>
 <section className="panel"><h2>Abrir comanda</h2><form onSubmit={e=>{e.preventDefault();void submit(async()=>{await api('/orders',token,'POST',{number,vehicleId,serviceId});setNumber('');});}}><input required placeholder="Número da comanda" value={number} onChange={e=>setNumber(e.target.value)}/><select required value={vehicleId} onChange={e=>setVehicleId(e.target.value)}><option value="">Veículo</option>{vehicles.map(v=><option key={v.id} value={v.id}>{v.plate} — {v.model}</option>)}</select><select required value={serviceId} onChange={e=>setServiceId(e.target.value)}><option value="">Serviço</option>{services.map(s=><option key={s.id} value={s.id}>{s.name} — {currency(s.price)}</option>)}</select><button disabled={busy}>Abrir comanda</button></form></section></div>
 <section className="panel"><h2>Gerenciar serviços</h2><form onSubmit={e=>{e.preventDefault();void submit(async()=>{const price=Number(servicePrice.replace(',','.'));if(!Number.isFinite(price)||price<0)throw new Error('Preço inválido');await api('/services'+(editingService?'/'+editingService:''),token,editingService?'PUT':'POST',{name:serviceName,price});setServiceName('');setServicePrice('');setEditingService(null);});}}><label>Nome do serviço<input required maxLength={160} value={serviceName} onChange={e=>setServiceName(e.target.value)} placeholder="Ex.: Lavagem ecológica"/></label><label>Preço (R$)<input required inputMode="decimal" value={servicePrice} onChange={e=>setServicePrice(e.target.value)} placeholder="45,00"/></label><button disabled={busy}>{editingService?'Salvar alterações':'Adicionar serviço'}</button>{editingService&&<button type="button" className="secondary" onClick={()=>{setEditingService(null);setServiceName('');setServicePrice('');}}>Cancelar edição</button>}</form><div className="tablewrap"><table><thead><tr><th>Serviço</th><th>Preço</th><th>Ações</th></tr></thead><tbody>{services.map(s=><tr key={s.id}><td>{s.name}</td><td>{currency(s.price)}</td><td><button type="button" className="secondary" onClick={()=>{setEditingService(s.id);setServiceName(s.name);setServicePrice(String(s.price));}}>Editar</button> <button type="button" className="secondary" disabled={busy} onClick={()=>{if(!window.confirm('Excluir '+s.name+'? Serviços usados em comandas não podem ser excluídos.'))return;void submit(async()=>{await api('/services/'+s.id,token,'DELETE');if(editingService===s.id){setEditingService(null);setServiceName('');setServicePrice('');}});}}>Excluir</button></td></tr>)}</tbody></table></div></section>
 <section className="panel"><h2>Comandas recentes</h2><div className="tablewrap"><table><thead><tr><th>Número</th><th>Status</th><th>Pagamento</th><th>Total</th></tr></thead><tbody>{orders.map(o=><tr key={o.id}><td>#{o.number}</td><td>{['Aguardando','Em lavagem','Acabamento','Pronto','Entregue','Cancelado'][o.status]??o.status}</td><td>{['Pendente','Parcial','Pago','Estornado'][o.paymentStatus]??o.paymentStatus}</td><td>{currency(o.total)}</td></tr>)}</tbody></table></div></section><footer>ATWash · Versão inicial de operação online · Não homologada para produção</footer></main>;
}
createRoot(document.getElementById('root')!).render(<App/>);
