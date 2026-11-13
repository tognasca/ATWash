import {useEffect,useState} from 'react';
import {api,currency} from './api';
import {useDialog} from './Dialog';
import PixAdmin from './PixAdmin';

type Device={id:string;name:string;userId:string;createdAt:string;revokedAt:string|null};
type Plan={id:string;name:string;monthlyPrice:number;maxDevices:number};
type Subscription={subscription:{planId:string;status:string;startsAt:string;expiresAt:string|null}|null};
type Billing={providerId:string;providerStatus:string;localStatus:string;createdAt:string;lastSyncedAt:string|null};
type Props={token:string;role:string;onError:(message:string)=>void};

export default function SettingsAdmin({token,role,onError}:Props){
 const dialog=useDialog();
 const [section,setSection]=useState<'pix'|'devices'|'subscription'>('pix');
 const [devices,setDevices]=useState<Device[]>([]);
 const [plans,setPlans]=useState<Plan[]>([]);
 const [subscription,setSubscription]=useState<Subscription|null>(null);
 const [billing,setBilling]=useState<Billing[]>([]);
 const [busy,setBusy]=useState(false);
 const [loaded,setLoaded]=useState(false);
 const owner=role==='Owner';
 async function refresh(){
  setBusy(true);
  try{
   const [d,p,s]=await Promise.all([
    api<Device[]>('/devices',token),
    api<Plan[]>('/saas/plans',token),
    api<Subscription>('/saas/subscription',token)
   ]);
   setDevices(d);setPlans(p);setSubscription(s);
   if(owner)setBilling(await api<Billing[]>('/saas/mercadopago/subscriptions',token));
   setLoaded(true);
  }catch(e){onError(String(e))}
  finally{setBusy(false)}
 }
 useEffect(()=>{void refresh()},[token]);
 async function revoke(d:Device){
  if(!await dialog.confirm('Revogar o acesso do dispositivo '+d.name+'? A sessão será invalidada imediatamente.'))return;
  setBusy(true);
  try{await api('/devices/'+d.id+'/revoke',token,'POST',{});dialog.show('Dispositivo revogado.','success');await refresh()}
  catch(e){onError(String(e))}finally{setBusy(false)}
 }
 async function checkout(){
  if(!await dialog.confirm('Iniciar uma assinatura recorrente mensal no Mercado Pago?'))return;
  setBusy(true);
  try{
   const result=await api<{checkoutUrl:string}>('/saas/mercadopago/checkout',token,'POST',{});
   const url=new URL(result.checkoutUrl);
   if(url.protocol!=='https:'||!(url.hostname==='mercadopago.com.br'||url.hostname.endsWith('.mercadopago.com.br')))
    throw new Error('Endereço de checkout inválido.');
   window.location.assign(url.toString());
  }catch(e){onError(String(e))}finally{setBusy(false)}
 }
 async function sync(id:string){
  setBusy(true);
  try{await api('/saas/mercadopago/sync/'+encodeURIComponent(id),token,'POST',{});await refresh();dialog.show('Status atualizado pelo Mercado Pago.','success')}
  catch(e){onError(String(e))}finally{setBusy(false)}
 }
 const currentPlan=plans.find(x=>x.id===subscription?.subscription?.planId);
 const active=devices.filter(x=>!x.revokedAt).length;
 return <div>
  <p>Gerencie pagamentos Pix, dispositivos autorizados e assinatura da empresa.</p>
  <nav className="actions" aria-label="Configurações">
   <button className={section==='pix'?'':'secondary'} onClick={()=>setSection('pix')}>Pix</button>
   <button className={section==='devices'?'':'secondary'} onClick={()=>setSection('devices')}>Dispositivos</button>
   <button className={section==='subscription'?'':'secondary'} onClick={()=>setSection('subscription')}>Plano e cobrança</button>
   <button className="secondary" disabled={busy} onClick={()=>void refresh()}>Atualizar</button>
  </nav>
  {section==='pix'&&<PixAdmin token={token} onError={onError}/>}
  {section==='devices'&&<section><h3>Dispositivos autorizados</h3>
   <p>{loaded?active+' dispositivo(s) ativo(s)'+(currentPlan?' de '+currentPlan.maxDevices+' permitidos':''):'Carregando dispositivos...'}</p>
   <div className="tablewrap"><table><thead><tr><th>Dispositivo</th><th>Cadastro</th><th>Situação</th><th>Ação</th></tr></thead><tbody>
    {devices.map(d=><tr key={d.id}><td>{d.name}</td><td>{new Date(d.createdAt).toLocaleString('pt-BR')}</td><td>{d.revokedAt?'Revogado':'Ativo'}</td>
     <td>{!d.revokedAt&&<button className="secondary" disabled={busy} onClick={()=>void revoke(d)}>Revogar</button>}</td></tr>)}
   </tbody></table></div></section>}
  {section==='subscription'&&<section><h3>Plano contratado</h3>
   <p>Plano: <strong>{currentPlan?.name??'Não identificado'}</strong></p>
   <p>Mensalidade: {currentPlan?currency(currentPlan.monthlyPrice):'Não disponível'}</p>
   <p>Dispositivos permitidos: {currentPlan?.maxDevices??'Não disponível'}</p>
   <p>Status: <strong>{subscription?.subscription?.status??'Sem assinatura'}</strong></p>
   <p>Vencimento: {subscription?.subscription?.expiresAt?new Date(subscription.subscription.expiresAt).toLocaleDateString('pt-BR'):'Não definido'}</p>
   {owner?<><button disabled={busy} onClick={()=>void checkout()}>Contratar / renovar pelo Mercado Pago</button>
    <h3>Histórico de cobrança</h3>
    <div className="tablewrap"><table><thead><tr><th>Assinatura</th><th>Provedor</th><th>Última conferência</th><th>Ação</th></tr></thead><tbody>
     {billing.map(b=><tr key={b.providerId}><td>{b.providerId}</td><td>{b.providerStatus} / {b.localStatus}</td><td>{b.lastSyncedAt?new Date(b.lastSyncedAt).toLocaleString('pt-BR'):'Pendente'}</td>
      <td><button className="secondary" disabled={busy} onClick={()=>void sync(b.providerId)}>Conferir</button></td></tr>)}
    </tbody></table></div>
    <p>A confirmação da assinatura depende da API do Mercado Pago. A configuração das credenciais é realizada pelo administrador da plataforma.</p>
   </>:<p>Somente o proprietário pode gerenciar a cobrança recorrente.</p>}
  </section>}
 </div>;
}
