import {useEffect,useState} from 'react';
import {api,currency} from './api';
import {useDialog} from './Dialog';
type DashboardData={orders:number;completed:number;open:number;cancelled:number;grossReceived:number;refunded:number;netReceived:number;averageTicket:number;estimatedSupplyCost:number;byDay:{date:string;orders:number;billed:number}[]};
type Device={id:string;name:string;createdAt:string;revokedAt?:string};
type Subscription={subscription:{status:string;planId:string;expiresAt?:string}|null;provisioning:string;billingIntegration:boolean};
export default function Dashboard({token,onError}:{token:string;onError:(x:string)=>void}){
 const dialog=useDialog();
 const [data,setData]=useState<DashboardData|null>(null);
 const [devices,setDevices]=useState<Device[]>([]);
 const [subscription,setSubscription]=useState<Subscription|null>(null);
 const [from,setFrom]=useState(()=>new Date(Date.now()-30*86400000).toISOString().slice(0,10));
 const [to,setTo]=useState(()=>new Date().toISOString().slice(0,10));
 async function load(){
  try{
   const [d,dev,sub]=await Promise.all([
    api<DashboardData>('/dashboard?from='+encodeURIComponent(from+'T00:00:00Z')+'&to='+encodeURIComponent(to+'T23:59:59Z'),token),
    api<Device[]>('/devices',token),api<Subscription>('/saas/subscription',token)
   ]);setData(d);setDevices(dev);setSubscription(sub)
  }catch(e){onError(String(e))}
 }
 useEffect(()=>{void load()},[token]);
 function exportCsv(){
  if(!data)return;
  const rows=[['Data','Comandas','Valor das comandas'],...data.byDay.map(x=>[x.date.slice(0,10),String(x.orders),x.billed.toFixed(2).replace('.',',')])];
  const csv='\\uFEFF'+rows.map(row=>row.map(value=>'"'+String(value).replace(/"/g,'""')+'"').join(';')).join('\\r\\n');
  const url=URL.createObjectURL(new Blob([csv],{type:'text/csv;charset=utf-8'}));
  const a=document.createElement('a');a.href=url;a.download='atwash-relatorio-'+from+'-'+to+'.csv';a.click();
  URL.revokeObjectURL(url);
 }
 async function revoke(id:string){if(!await dialog.confirm('Revogar este dispositivo? O acesso será bloqueado imediatamente.'))return;try{await api('/devices/'+id+'/revoke',token,'POST',{});await load();dialog.show('Dispositivo revogado com sucesso.','success')}catch(e){onError(String(e))}}
 return <section className="panel"><h2>Indicadores e gestão SaaS</h2>
 <div className="actions"><label>De<input type="date" value={from} onChange={e=>setFrom(e.target.value)}/></label><label>Até<input type="date" value={to} onChange={e=>setTo(e.target.value)}/></label><button onClick={()=>void load()}>Atualizar</button></div>
 {data&&<><div className="kanban-board">{[
 ['Comandas',data.orders],['Entregues',data.completed],['Em aberto',data.open],['Canceladas',data.cancelled]
 ].map(([label,value])=><article className="kanban-card" key={label}><strong>{label}</strong><span>{value}</span></article>)}</div>
 <div className="kanban-board">{[
 ['Recebido bruto',currency(data.grossReceived)],['Estornos',currency(data.refunded)],
 ['Recebido líquido',currency(data.netReceived)],['Ticket médio',currency(data.averageTicket)]
 ].map(([label,value])=><article className="kanban-card" key={label}><strong>{label}</strong><span>{value}</span></article>)}</div>
 <p>Custo estimado de insumos: {currency(data.estimatedSupplyCost)} (custo médio atual, não contabilidade).</p>
 <h3>Movimento diário</h3><button className="secondary" onClick={exportCsv}>Exportar CSV (Excel)</button><div className="tablewrap"><table><thead><tr><th>Dia</th><th>Comandas</th><th>Valor das comandas</th></tr></thead><tbody>{data.byDay.map(x=><tr key={x.date}><td>{x.date.slice(0,10)}</td><td>{x.orders}</td><td>{currency(x.billed)}</td></tr>)}</tbody></table></div></>}
 <h3>Dispositivos cadastrados</h3><p>Revogar um dispositivo bloqueia seus tokens imediatamente. Para autorizar novamente, é necessário cadastrar outro identificador.</p>
 <div className="tablewrap"><table><thead><tr><th>Dispositivo</th><th>Cadastro</th><th>Status</th><th>Ação</th></tr></thead><tbody>{devices.map(d=><tr key={d.id}><td>{d.name}</td><td>{new Date(d.createdAt).toLocaleDateString('pt-BR')}</td><td>{d.revokedAt?'Revogado':'Ativo'}</td><td>{!d.revokedAt&&<button className="secondary" onClick={()=>void revoke(d.id)}>Revogar</button>}</td></tr>)}</tbody></table></div>
 <h3>Assinatura da empresa</h3><p>{subscription?.subscription?'Status: '+subscription.subscription.status:'Sem assinatura configurada.'} Cobrança e provisionamento ainda manuais; nenhum pagamento recorrente automático está ativo.</p>
 </section>
}
