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
 async function startSubscription(){
  try{
   const result=await api<{checkoutUrl:string}>('/saas/mercadopago/checkout',token,'POST',{});
   const url=new URL(result.checkoutUrl);
   if(url.protocol!=='https:'||!(url.hostname==='mercadopago.com.br'||url.hostname.endsWith('.mercadopago.com.br')))
    throw new Error('Endereço de pagamento não autorizado.');
   window.open(url.toString(),'_blank','noopener,noreferrer');
   dialog.show('Conclua a autorização no Mercado Pago. O retorno ao site não confirma pagamento; a assinatura precisa ser validada.','info');
  }catch(e){onError(String(e))}
 }
 async function revoke(id:string){if(!await dialog.confirm('Revogar este dispositivo? O acesso será bloqueado imediatamente.'))return;try{await api('/devices/'+id+'/revoke',token,'POST',{});await load();dialog.show('Dispositivo revogado com sucesso.','success')}catch(e){onError(String(e))}}
 const maxBilled=Math.max(1,...(data?.byDay.map(x=>x.billed)??[]));
 return <div className="aw-dashboard">
  <section className="aw-dashboard-hero"><div><span className="aw-dashboard-eyebrow">VISÃO GERAL DO NEGÓCIO</span><h2>Indicadores e gestão SaaS</h2><p>Acompanhe resultados, movimentação financeira e os dispositivos da sua operação.</p></div><div className="aw-dashboard-hero-icon" aria-hidden="true">▥</div></section>
  <section className="aw-dashboard-filters" aria-label="Período do relatório"><div className="aw-dashboard-filter-fields"><label>Data inicial<input type="date" value={from} max={to} onChange={e=>setFrom(e.target.value)}/></label><label>Data final<input type="date" value={to} min={from} onChange={e=>setTo(e.target.value)}/></label></div><button disabled={from>to} onClick={()=>void load()}>Atualizar indicadores</button>{data&&<button className="secondary" onClick={exportCsv}>↓ Exportar CSV</button>}</section>
  {data&&<><section className="aw-dashboard-metrics" aria-label="Indicadores operacionais">{[
   {label:'Total de comandas',value:data.orders,icon:'▤',tone:'purple'},
   {label:'Entregues',value:data.completed,icon:'✓',tone:'green'},
   {label:'Em aberto',value:data.open,icon:'◷',tone:'blue'},
   {label:'Canceladas',value:data.cancelled,icon:'×',tone:'orange'}
  ].map(item=><article className={'aw-metric aw-metric-'+item.tone} key={item.label}><span className="aw-metric-icon" aria-hidden="true">{item.icon}</span><small>{item.label}</small><strong>{item.value}</strong></article>)}</section>
  <section className="aw-dashboard-finances" aria-label="Resumo financeiro">{[
   {label:'Recebido bruto',value:currency(data.grossReceived),tone:'purple'},
   {label:'Estornos',value:currency(data.refunded),tone:'orange'},
   {label:'Recebido líquido',value:currency(data.netReceived),tone:'green'},
   {label:'Ticket médio',value:currency(data.averageTicket),tone:'blue'}
  ].map(item=><article className={'aw-finance-metric aw-finance-'+item.tone} key={item.label}><small>{item.label}</small><strong>{item.value}</strong><span className="aw-finance-bar"/></article>)}</section>
  <section className="aw-dashboard-card"><div className="aw-dashboard-card-head"><div><h3>Movimento diário</h3><p>Valor das comandas por dia no período selecionado</p></div><span className="aw-dashboard-chip">{data.byDay.length} dias com registros</span></div>
  {data.byDay.length?<><div className="aw-daily-bars" role="img" aria-label="Gráfico de valores das comandas por dia">{data.byDay.map(day=><div className="aw-daily-column" key={day.date} title={day.date.slice(0,10)+' — '+currency(day.billed)}><div className="aw-daily-bar" style={{height:Math.max(4,day.billed/maxBilled*100)+'%'}}/><small>{day.date.slice(5,10).replace('-','/')}</small></div>)}</div><div className="tablewrap aw-dashboard-table"><table><thead><tr><th>Dia</th><th>Comandas</th><th>Valor das comandas</th></tr></thead><tbody>{data.byDay.map(x=><tr key={x.date}><td>{new Date(x.date.slice(0,10)+'T12:00:00').toLocaleDateString('pt-BR')}</td><td>{x.orders}</td><td><strong>{currency(x.billed)}</strong></td></tr>)}</tbody></table></div></>:<p className="aw-dashboard-empty">Nenhuma movimentação registrada neste período.</p>}
  <p className="aw-dashboard-footnote">Custo estimado de insumos: <strong>{currency(data.estimatedSupplyCost)}</strong> — calculado pelo custo médio atual, não representa contabilidade.</p></section></>}
  <div className="aw-dashboard-bottom"><section className="aw-dashboard-card"><div className="aw-dashboard-card-head"><div><h3>Dispositivos cadastrados</h3><p>Controle os tablets e celulares autorizados.</p></div><span className="aw-dashboard-chip">{devices.filter(d=>!d.revokedAt).length} ativos</span></div><div className="tablewrap aw-dashboard-table"><table><thead><tr><th>Dispositivo</th><th>Cadastro</th><th>Status</th><th>Ação</th></tr></thead><tbody>{devices.map(d=><tr key={d.id}><td><strong>{d.name}</strong></td><td>{new Date(d.createdAt).toLocaleDateString('pt-BR')}</td><td><span className={'aw-device-status '+(d.revokedAt?'aw-device-revoked':'aw-device-active')}>{d.revokedAt?'Revogado':'Ativo'}</span></td><td>{!d.revokedAt&&<button className="secondary" onClick={()=>void revoke(d.id)}>Revogar</button>}</td></tr>)}</tbody></table></div>{!devices.length&&<p className="aw-dashboard-empty">Nenhum dispositivo cadastrado.</p>}<p className="aw-dashboard-footnote">Revogar bloqueia imediatamente os tokens do dispositivo.</p></section>
  <section className="aw-dashboard-card aw-subscription-card"><div className="aw-dashboard-card-head"><div><h3>Assinatura da empresa</h3><p>Plano e cobrança recorrente</p></div></div><div className="aw-subscription-visual"><span aria-hidden="true">◇</span><small>Status atual</small><strong>{subscription?.subscription?.status??'Sem assinatura configurada'}</strong></div><p>A cobrança recorrente depende da autorização e confirmação pelo Mercado Pago.</p>{subscription?.subscription&&<button onClick={()=>void startSubscription()}>Autorizar mensalidade no Mercado Pago</button>}</section></div>
 </div>;
}
