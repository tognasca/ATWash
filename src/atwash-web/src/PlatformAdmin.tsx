import {useState,type FormEvent} from 'react';
import {API_BASE,currency} from './api';
import {useDialog} from './Dialog';

type Plan={id:string;name:string;monthlyPrice:number;maxDevices:number;isActive:boolean};
type Tenant={id:string;name:string;isActive:boolean;isDemo:boolean;subscription?:{planId:string;status:string;expiresAt?:string}};
export default function PlatformAdmin(){
 const dialog=useDialog();
 const [key,setKey]=useState('');
 const [plans,setPlans]=useState<Plan[]>([]);
 const [tenants,setTenants]=useState<Tenant[]>([]);
 const [loading,setLoading]=useState(false);
 const [name,setName]=useState('');const [price,setPrice]=useState('99.90');const [devices,setDevices]=useState('3');
 const [company,setCompany]=useState('');const [email,setEmail]=useState('');const [password,setPassword]=useState('');const [planId,setPlanId]=useState('');
 async function call<T>(path:string,method='GET',body?:unknown):Promise<T>{
  if(location.protocol!=='https:'&&!['localhost','127.0.0.1'].includes(location.hostname))throw new Error('Painel administrativo exige HTTPS.');
  if(!key||key.length<32)throw new Error('Informe a chave administrativa da plataforma.');
  const response=await fetch(API_BASE+'/platform'+path,{method,headers:{'X-ATWash-Platform-Key':key,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),cache:'no-store'});
  if(!response.ok)throw new Error('Falha administrativa ('+response.status+'): '+(await response.text()).slice(0,200));
  return response.json() as Promise<T>;
 }
 async function refresh(){
  setLoading(true);
  try{const [p,t]=await Promise.all([call<Plan[]>('/plans'),call<Tenant[]>('/tenants')]);setPlans(p);setTenants(t);if(!planId&&p.length)setPlanId(p[0].id)}
  catch(e){dialog.show(String(e),'error')}finally{setLoading(false)}
 }
 async function addPlan(e:FormEvent){e.preventDefault();try{
  await call('/plans','POST',{name,monthlyPrice:Number(price),maxDevices:Number(devices),isActive:true});
  setName('');await refresh();dialog.show('Plano cadastrado.','success');
 }catch(e){dialog.show(String(e),'error')}}
 async function addTenant(e:FormEvent){e.preventDefault();try{
  await call('/tenants','POST',{name:company,ownerEmail:email,ownerPassword:password,planId,expiresAt:null});
  setCompany('');setEmail('');setPassword('');await refresh();dialog.show('Empresa cadastrada.','success');
 }catch(e){dialog.show(String(e),'error')}}
 async function changeTenant(t:Tenant,active:boolean){if(!await dialog.confirm((active?'Reativar ':'Suspender ')+t.name+'?'))return;try{
  await call('/tenants/'+t.id+'/status?active='+active,'PUT');await refresh();
 }catch(e){dialog.show(String(e),'error')}}
 async function changeSubscription(t:Tenant,status:string,selectedPlan:string){
  if(!await dialog.confirm('Alterar assinatura de '+t.name+' para '+status+'?'))return;
  try{await call('/tenants/'+t.id+'/subscription','PUT',{planId:selectedPlan,status,expiresAt:null});await refresh()}
  catch(e){dialog.show(String(e),'error')}
 }
 return <main className="panel"><h1>ATWashControl — Plataforma SaaS</h1>
 <p>Administração global. A chave é mantida somente na memória desta página, nunca no armazenamento local.</p>
 <div className="actions"><label>Chave da plataforma<input type="password" autoComplete="off" value={key} onChange={e=>setKey(e.target.value)}/></label><button disabled={loading||key.length<32} onClick={()=>void refresh()}>Carregar empresas e planos</button><button className="secondary" onClick={()=>{setKey('');setPlans([]);setTenants([])}}>Bloquear painel</button></div>
 <h2>Planos</h2><form className="actions" onSubmit={addPlan}><label>Nome<input required value={name} onChange={e=>setName(e.target.value)}/></label><label>Mensalidade (R$)<input type="number" min="0" step="0.01" required value={price} onChange={e=>setPrice(e.target.value)}/></label><label>Dispositivos<input type="number" min="1" required value={devices} onChange={e=>setDevices(e.target.value)}/></label><button disabled={!key}>Criar plano</button></form>
 <div className="tablewrap"><table><thead><tr><th>Plano</th><th>Mensalidade</th><th>Dispositivos</th><th>Ativo</th></tr></thead><tbody>{plans.map(p=><tr key={p.id}><td>{p.name}</td><td>{currency(p.monthlyPrice)}</td><td>{p.maxDevices}</td><td>{p.isActive?'Sim':'Não'}</td></tr>)}</tbody></table></div>
 <h2>Nova empresa</h2><form className="actions" onSubmit={addTenant}><label>Empresa<input required value={company} onChange={e=>setCompany(e.target.value)}/></label><label>E-mail proprietário<input type="email" required value={email} onChange={e=>setEmail(e.target.value)}/></label><label>Senha inicial (12+)<input type="password" minLength={12} required value={password} onChange={e=>setPassword(e.target.value)}/></label><label>Plano<select required value={planId} onChange={e=>setPlanId(e.target.value)}>{plans.filter(p=>p.isActive).map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select></label><button disabled={!key||!planId}>Cadastrar empresa</button></form>
 <h2>Empresas e assinaturas</h2><div className="tablewrap"><table><thead><tr><th>Empresa</th><th>Status</th><th>Plano</th><th>Assinatura</th><th>Ações</th></tr></thead><tbody>{tenants.map(t=><tr key={t.id}><td>{t.name}</td><td>{t.isActive?'Ativa':'Suspensa'}</td><td><select aria-label={'Plano de '+t.name} value={t.subscription?.planId??''} onChange={e=>void changeSubscription(t,t.subscription?.status??'active',e.target.value)}>{!t.subscription&&<option value="">Sem plano</option>}{plans.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select></td><td><select aria-label={'Assinatura de '+t.name} value={t.subscription?.status??''} onChange={e=>void changeSubscription(t,e.target.value,t.subscription?.planId??planId)}><option value="">Sem assinatura</option>{['trial','active','past_due','suspended','cancelled'].map(s=><option key={s} value={s}>{s}</option>)}</select></td><td><button className="secondary" onClick={()=>void changeTenant(t,!t.isActive)}>{t.isActive?'Suspender':'Reativar'}</button></td></tr>)}</tbody></table></div>
 <p>Alterações manuais de assinatura não comprovam pagamentos. Cobrança recorrente exige reconciliação independente com o Mercado Pago.</p>
 </main>;
}
