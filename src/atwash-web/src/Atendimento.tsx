import {useEffect,useState} from 'react';
import {useDialog} from './Dialog';
import PaymentPanel from './PaymentPanel';
import {queueStatus,tenantFromToken,createOfflineCustomer,createOfflineVehicle,createOfflineOrder} from './offline';
import {api,currency,type Customer,type Vehicle,type Service,type Product,type Order} from './api';
type Props={token:string;customers:Customer[];vehicles:Vehicle[];services:Service[];products:Product[];orders:Order[];reload:()=>Promise<void>;onError:(error:string)=>void;canConfirmPix:boolean;canWrite:boolean;online:boolean;onQueued:()=>void};
export default function Atendimento({token,customers,vehicles,services,products,orders,reload,onError,canConfirmPix,canWrite,online,onQueued}:Props){
 const dialog=useDialog();
 const [employees,setEmployees]=useState<{id:string;name:string;isActive:boolean}[]>([]);
 useEffect(()=>{if(!online)return;void api<typeof employees>('/employees',token).then(setEmployees).catch(()=>{})},[token,online]);
 const [query,setQuery]=useState('');const [matches,setMatches]=useState<Customer[]>([]);
 const [customer,setCustomer]=useState<Customer|null>(null);const [vehicleId,setVehicleId]=useState('');
 const [name,setName]=useState('');const [phone,setPhone]=useState('');const [plate,setPlate]=useState('');const [model,setModel]=useState('');
 const [selected,setSelected]=useState<string[]>([]);const [extra,setExtra]=useState<Record<string,string>>({});
 const [busy,setBusy]=useState(false);
 const [selectedProduct,setSelectedProduct]=useState<Record<string,string>>({});const [productQty,setProductQty]=useState<Record<string,string>>({});const [consume,setConsume]=useState<Record<string,boolean>>({});
 const [paymentOrderId,setPaymentOrderId]=useState<string|null>(null);
 const [showAllOrders,setShowAllOrders]=useState(false);
 const [catalogTab,setCatalogTab]=useState<'services'|'products'>('services');
 const [draftQty,setDraftQty]=useState<Record<string,string>>({});
 const [catalogSearch,setCatalogSearch]=useState('');
 const [cartOpen,setCartOpen]=useState(false);
 const [activeOrderId,setActiveOrderId]=useState<string|null>(null);
 async function run(fn:()=>Promise<unknown>){setBusy(true);onError('');try{await fn();await reload();dialog.show('Operação realizada com sucesso.','success')}catch(e){onError(String(e))}finally{setBusy(false)}}
 async function advance(order:Order){
  if(online){await run(()=>api('/orders/'+order.id+'/status',token,'PATCH',{status:order.status+1}));return}
  await run(async()=>{await queueStatus(tenantFromToken(token),order.id,order.status,order.status+1);onQueued();dialog.show('Etapa registrada offline. Será enviada quando a conexão voltar.','info')});
 }
 async function search(){if(query.trim().length<2){onError('Digite pelo menos 2 caracteres');return}await run(async()=>{const found=online?await api<Customer[]>('/customers/search?q='+encodeURIComponent(query),token):customers.filter(c=>c.name.toLowerCase().includes(query.toLowerCase())||c.phone?.includes(query)||vehicles.some(v=>v.customerId===c.id&&v.plate.toLowerCase().includes(query.toLowerCase()))).map(c=>({...c,vehicles:vehicles.filter(v=>v.customerId===c.id)}));setMatches(found);if(found.length===0)setCustomer(null)})}
 const clientVehicles=vehicles.filter(v=>v.customerId===customer?.id);
 const paymentOrder=orders.find(o=>o.id===paymentOrderId);
 const draftServices=services.filter(s=>selected.includes(s.id));
 const draftTotal=draftServices.reduce((sum,s)=>sum+s.price,0);
 const activeOrder=orders.find(o=>o.id===activeOrderId);
 const visibleServices=services.filter(s=>s.name.toLowerCase().includes(catalogSearch.toLowerCase()));
 const visibleProducts=products.filter(p=>p.name.toLowerCase().includes(catalogSearch.toLowerCase()));
 return <>{paymentOrder&&<PaymentPanel key={paymentOrder.id} order={paymentOrder} token={token} onClose={()=>setPaymentOrderId(null)} onChanged={reload} onError={onError} canConfirmPix={canConfirmPix}/>}<section className="aw-attendance">
 <div className="aw-heading"><div><h2>Atendimento</h2><p>Escolha o cliente, o veículo e os serviços para abrir a comanda.</p></div><span className={online?'aw-connect aw-on':'aw-connect aw-off'}>{online?'● Online':'● Offline'}</span></div>
 {!online&&<p role="status" className="aw-notice">Modo offline: clientes, veículos, comandas e etapas de lavagem são salvos localmente. Produtos, serviços extras e pagamentos exigem conexão.</p>}
 <div className="aw-kpis">
 <article><strong>{orders.filter(o=>o.status<4).length}</strong><span>Em atendimento</span></article>
 <article><strong>{orders.filter(o=>o.status===1||o.status===2).length}</strong><span>Em execução</span></article>
 <article><strong>{orders.filter(o=>o.status===4).length}</strong><span>Concluídas</span></article>
 <article><strong>{orders.filter(o=>o.status===3).length}</strong><span>Prontas</span></article>
 </div>
 <div className="aw-workspace">
 <div className="aw-left">
 <section className="aw-customer panel">
 <div className="aw-section-title"><h3>Cliente e veículo</h3>{customer&&<button type="button" className="secondary" onClick={()=>{setCustomer(null);setVehicleId('');setSelected([]);setActiveOrderId(null)}}>Trocar cliente</button>}</div>
 <div className="actions"><input aria-label="Buscar cliente ou placa" placeholder="Buscar nome, placa ou telefone" value={query} onChange={e=>setQuery(e.target.value)} onKeyDown={e=>{if(e.key==='Enter'){e.preventDefault();void search()}}}/><button type="button" disabled={busy} onClick={()=>void search()}>Buscar</button></div>
 {matches.length>0&&<div className="aw-customer-matches">{matches.map(c=><button key={c.id} type="button" className="secondary" onClick={()=>{setCustomer(c);setVehicleId(c.vehicles?.[0]?.id||vehicles.find(v=>v.customerId===c.id)?.id||'');setMatches([]);setActiveOrderId(null)}}><strong>{c.name}</strong><span>{c.phone||'Sem telefone'} · {c.vehicles?.map(v=>v.plate).join(', ')||'Sem veículo'}</span></button>)}</div>}
 {customer?<div className="aw-identity"><div><small>Cliente selecionado</small><strong>{customer.name}</strong><span>{customer.phone||'Sem telefone'}</span></div><label>Veículo<select value={vehicleId} onChange={e=>{setVehicleId(e.target.value);setActiveOrderId(null)}}><option value="">Selecione o veículo</option>{clientVehicles.map(v=><option key={v.id} value={v.id}>{v.plate} — {v.model}</option>)}</select></label></div>:<p>Pesquise um cliente existente ou cadastre um novo abaixo.</p>}
 {canWrite&&!customer&&<form className="aw-mini-form" onSubmit={e=>{e.preventDefault();void run(async()=>{const created=online?await api<{id:string}>('/customers',token,'POST',{name,phone}):await createOfflineCustomer(tenantFromToken(token),name,phone);setCustomer({id:created.id,name,phone});if(!online)onQueued();setName('');setPhone('')})}}><h3>Novo cliente</h3><input required placeholder="Nome completo" value={name} onChange={e=>setName(e.target.value)}/><input placeholder="Telefone" value={phone} onChange={e=>setPhone(e.target.value)}/><button disabled={busy}>Cadastrar cliente</button></form>}
 {canWrite&&customer&&<details className="aw-add-vehicle"><summary>+ Adicionar veículo ao cliente</summary><form className="aw-mini-form" onSubmit={e=>{e.preventDefault();void run(async()=>{const created=online?await api<{id:string}>('/vehicles',token,'POST',{customerId:customer.id,plate,model}):await createOfflineVehicle(tenantFromToken(token),customer.id,plate,model);setVehicleId(created.id);if(!online)onQueued();setPlate('');setModel('')})}}><input required placeholder="Placa" value={plate} onChange={e=>setPlate(e.target.value)}/><input required placeholder="Modelo" value={model} onChange={e=>setModel(e.target.value)}/><button disabled={busy}>Adicionar veículo</button></form></details>}
 </section>
 <section className="panel aw-catalog">
 <div className="aw-section-title"><h3>Escolha os itens</h3><span>{selected.length} serviço(s)</span></div>
 <div className="aw-tabs"><button type="button" className={catalogTab==='services'?'aw-active':'secondary'} onClick={()=>{setCatalogTab('services');setCatalogSearch('')}}>Serviços</button><button type="button" className={catalogTab==='products'?'aw-active':'secondary'} onClick={()=>{setCatalogTab('products');setCatalogSearch('')}}>Produtos / Extras</button></div>
 <input className="aw-catalog-search" aria-label="Filtrar itens" placeholder="Pesquisar no catálogo..." value={catalogSearch} onChange={e=>setCatalogSearch(e.target.value)}/>
 <div className="aw-service-grid">
 {catalogTab==='services'?visibleServices.map((s,i)=><button type="button" key={s.id} disabled={!canWrite} aria-pressed={selected.includes(s.id)} className={'aw-service-card '+(selected.includes(s.id)?'aw-selected':'')} onClick={()=>setSelected(old=>old.includes(s.id)?old.filter(id=>id!==s.id):[...old,s.id])}><span className={'aw-service-illustration aw-ill-'+i%6} aria-hidden="true">{['🚘','🫧','🧽','✨','💧','🧼'][i%6]}</span><span className="aw-service-body"><strong>{s.name}</strong><span className="aw-service-bottom"><b>{currency(s.price)}</b><span className="aw-add-icon">{selected.includes(s.id)?'✓':'+'}</span></span></span></button>):visibleProducts.map((p,i)=><article className="aw-service-card" key={p.id}><span className={'aw-service-illustration aw-ill-'+i%6} aria-hidden="true">{p.isSupply?'🧴':'📦'}</span><div className="aw-service-body"><strong>{p.name}</strong><small>Estoque: {p.stock} {p.unit}</small><span className="aw-service-bottom"><b>{currency(p.price)}</b></span><div className="aw-quantity"><input aria-label={'Quantidade '+p.name} inputMode="decimal" value={draftQty[p.id]??'1'} onChange={e=>setDraftQty(v=>({...v,[p.id]:e.target.value}))}/><button type="button" disabled={!canWrite||!online||!activeOrder||activeOrder.status>=4||activeOrder.paymentStatus!==0||busy} onClick={()=>void run(async()=>{if(!activeOrder)throw new Error('Selecione uma comanda em andamento.');const quantity=Number((draftQty[p.id]??'1').replace(',','.'));if(!Number.isFinite(quantity)||quantity<=0)throw new Error('Quantidade inválida');await api('/orders/'+activeOrder.id+'/products',token,'POST',{productId:p.id,quantity,isConsumption:false})})}>+ Adicionar</button></div></div></article>)}
 </div>
 {catalogTab==='products'&&<p className="aw-hint">Produtos são adicionados a uma comanda já aberta. Selecione a comanda no painel à direita. Operação disponível online.</p>}
 </section>
 </div>
 <aside className={'aw-cart panel '+(cartOpen?'aw-cart-open':'')} id="aw-cart">
 <div className="aw-section-title"><h3>{activeOrder?'Comanda #'+activeOrder.number:'Nova comanda'}</h3><button type="button" className="secondary aw-cart-close" onClick={()=>setCartOpen(false)}>Fechar</button></div>
 {customer&&<div className="aw-cart-customer"><strong>{customer.name}</strong><span>{vehicles.find(v=>v.id===vehicleId)?.plate||'Selecione o veículo'}</span></div>}
 {activeOrder?<><div className="aw-cart-line"><strong>Comanda em andamento</strong><span>{['Aguardando','Lavando','Acabamento','Pronto','Entregue','Cancelado'][activeOrder.status]||'—'}</span></div><div className="aw-cart-total"><span>Total atual</span><strong>{currency(activeOrder.total)}</strong></div><button type="button" className="aw-checkout" disabled={!online||!canWrite||activeOrder.status>=4} onClick={()=>{setPaymentOrderId(activeOrder.id);setCartOpen(false)}}>Receber / fechar</button><button type="button" className="secondary" onClick={()=>{setActiveOrderId(null);setSelected([]);}}>Nova comanda</button></>:<><p className="aw-hint">Selecione os serviços para montar sua comanda.</p><div className="aw-cart-items">{draftServices.length?draftServices.map(s=><div className="aw-cart-line" key={s.id}><span>{s.name}</span><strong>{currency(s.price)}</strong><button type="button" className="aw-remove" aria-label={'Remover '+s.name} onClick={()=>setSelected(v=>v.filter(id=>id!==s.id))}>×</button></div>):<div className="aw-empty">Nenhum serviço selecionado</div>}</div><div className="aw-cart-total"><span>Total</span><strong>{currency(draftTotal)}</strong></div><button type="button" className="aw-checkout" disabled={busy||!canWrite||!vehicleId||selected.length===0} onClick={()=>void run(async()=>{if(!vehicleId||selected.length===0)throw new Error('Selecione veículo e serviços');const result=online?await api<{id:string}>('/orders',token,'POST',{vehicleId,serviceIds:selected}):await createOfflineOrder(tenantFromToken(token),vehicleId,selected);setSelected([]);setActiveOrderId(result.id);setCartOpen(false);if(!online)onQueued()})}>Abrir comanda · {currency(draftTotal)}</button></>}
 <div className="aw-cart-footer">O número da comanda é gerado automaticamente.</div>
 </aside>
 </div>
 <button type="button" className="aw-mobile-cart" onClick={()=>setCartOpen(v=>!v)}>{cartOpen?'Fechar comanda':'Ver comanda'} <strong>{currency(activeOrder?.total??draftTotal)}</strong></button>
 </section>
 <section className="panel"><h2>Comandas em atendimento</h2>
 <div className="kanban-board">{[0,1,2,3].map(status=><div className="kanban-column" key={status}><h3>{['Aguardando','Em lavagem','Acabamento','Pronto'][status]} ({orders.filter(o=>o.status===status).length})</h3>{orders.filter(o=>o.status===status).map(o=><article className="kanban-card" key={o.id}><strong>#{o.number} · {vehicles.find(v=>v.id===o.vehicleId)?.plate||'—'}</strong><span>{currency(o.total)}</span><small>{employees.find(e=>e.id===o.employeeId)?.name||'Sem responsável'}</small>{status<3&&<button disabled={busy} onClick={()=>void advance(o)}>{['Iniciar','Acabamento','Marcar pronto'][status]}</button>}</article>)}</div>)}</div>
 <div className="actions"><h3>Lista de comandas</h3><button className="secondary" onClick={()=>setShowAllOrders(x=>!x)}>{showAllOrders?'Somente em andamento':'Mostrar todas'}</button></div>
 <div className="tablewrap order-cards"><table><thead><tr><th>Comanda</th><th>Veículo</th><th>Responsável</th><th>Etapa</th><th>Valor</th><th>Serviço extra</th><th>Produtos / insumos</th><th>Ação</th><th>Pagamento</th></tr></thead><tbody>{orders.filter(o=>showAllOrders||o.status<4).map(o=><tr key={o.id}><td>#{o.number}</td><td>{vehicles.find(v=>v.id===o.vehicleId)?.plate||'—'}</td><td><select aria-label={'Responsável '+o.number} disabled={!canWrite||!online||o.status>=4} value={o.employeeId||''} onChange={e=>void run(()=>api('/orders/'+o.id+'/employee',token,'PUT',{employeeId:e.target.value||null}))}><option value="">Não atribuído</option>{employees.filter(e=>e.isActive||e.id===o.employeeId).map(e=><option key={e.id} value={e.id}>{e.name}</option>)}</select></td><td>{['Aguardando','Lavando','Acabamento','Pronto','Entregue','Cancelado'][o.status]||'—'}</td><td>{currency(o.total)}</td><td>{canWrite&&o.status<4&&o.paymentStatus===0&&<div className="actions"><select aria-label={'Serviço extra '+o.number} value={extra[o.id]||''} onChange={e=>setExtra(old=>({...old,[o.id]:e.target.value}))}><option value="">Adicionar serviço</option>{services.map(s=><option key={s.id} value={s.id}>{s.name} — {currency(s.price)}</option>)}</select><button className="secondary" disabled={busy||!online||!extra[o.id]} onClick={()=>void run(async()=>{await api('/orders/'+o.id+'/services',token,'POST',{serviceId:extra[o.id]});setExtra(old=>({...old,[o.id]:''}))})}>+</button></div>}</td><td>{o.status<4&&o.paymentStatus===0&&<div className="actions">
 <select aria-label={'Produto '+o.number} value={selectedProduct[o.id]||''} onChange={e=>setSelectedProduct(v=>({...v,[o.id]:e.target.value}))}><option value="">Produto</option>{products.map(p=><option key={p.id} value={p.id}>{p.name} · {currency(p.price)} · saldo {p.stock}</option>)}</select>
 <input aria-label={'Quantidade '+o.number} inputMode="decimal" style={{width:70}} placeholder="Qtd." value={productQty[o.id]??'1'} onChange={e=>setProductQty(v=>({...v,[o.id]:e.target.value}))}/>
 {canConfirmPix&&<label><input type="checkbox" checked={!!consume[o.id]} onChange={e=>setConsume(v=>({...v,[o.id]:e.target.checked}))}/>Insumo</label>}
 <button className="secondary" disabled={busy||!online||!selectedProduct[o.id]} onClick={()=>void run(async()=>{
  const quantity=Number((productQty[o.id]??'1').replace(',','.'));
  if(!Number.isFinite(quantity)||quantity<=0)throw new Error('Quantidade inválida.');
  await api('/orders/'+o.id+'/products',token,'POST',{productId:selectedProduct[o.id],quantity,isConsumption:!!consume[o.id]});
  setSelectedProduct(v=>({...v,[o.id]:''}));
 })}>Adicionar</button></div>}</td><td>{o.status<3?<button disabled={busy} onClick={()=>void advance(o)}>{['Iniciar','Acabamento','Marcar pronto'][o.status]}</button>:o.status===3?<span>Pronto · Aguardando pagamento</span>:null}</td><td>{canWrite&&o.status<4&&<button className="secondary" disabled={!online} onClick={()=>setPaymentOrderId(o.id)}>{o.paymentStatus===2?'Ver pagamento':'Receber / fechar'}</button>}</td></tr>)}</tbody></table></div></section></>;
}
