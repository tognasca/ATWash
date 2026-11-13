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
 const [query,setQuery]=useState('');
 const [customer,setCustomer]=useState<Customer|null>(null);const [vehicleId,setVehicleId]=useState('');
 const [name,setName]=useState('');const [phone,setPhone]=useState('');const [plate,setPlate]=useState('');const [model,setModel]=useState('');
 const [selected,setSelected]=useState<string[]>([]);const [extra,setExtra]=useState<Record<string,string>>({});
 const [busy,setBusy]=useState(false);
 const [selectedProduct,setSelectedProduct]=useState<Record<string,string>>({});const [productQty,setProductQty]=useState<Record<string,string>>({});const [consume,setConsume]=useState<Record<string,boolean>>({});
 const [paymentOrderId,setPaymentOrderId]=useState<string|null>(null);
 const [orderFilter,setOrderFilter]=useState('active');
 const [orderQuery,setOrderQuery]=useState('');
 const [expandedOrder,setExpandedOrder]=useState<string|null>(null);
 const [catalogTab,setCatalogTab]=useState<'services'|'products'>('services');
 const [draftQty,setDraftQty]=useState<Record<string,string>>({});
 const [catalogSearch,setCatalogSearch]=useState('');
 const [creatingCustomer,setCreatingCustomer]=useState(false);
 const [cartOpen,setCartOpen]=useState(false);
 const [activeOrderId,setActiveOrderId]=useState<string|null>(null);
 async function run(fn:()=>Promise<unknown>){setBusy(true);onError('');try{await fn();await reload();dialog.show('Operação realizada com sucesso.','success')}catch(e){onError(String(e))}finally{setBusy(false)}}
 async function advance(order:Order){
  if(online){await run(()=>api('/orders/'+order.id+'/status',token,'PATCH',{status:order.status+1}));return}
  await run(async()=>{await queueStatus(tenantFromToken(token),order.id,order.status,order.status+1);onQueued();dialog.show('Etapa registrada offline. Será enviada quando a conexão voltar.','info')});
 }

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
 <div className="aw-section-title"><h3>1. Quem chegou?</h3>{customer&&<button type="button" className="secondary" onClick={()=>{setCustomer(null);setVehicleId('');setSelected([]);setActiveOrderId(null);setQuery('');setCreatingCustomer(false)}}>Trocar</button>}</div>
 {!customer?<><label className="aw-search-label">Digite a placa, nome ou telefone<input autoComplete="off" aria-label="Encontrar cliente" placeholder="Ex.: ABC1234 ou João" value={query} onChange={e=>setQuery(e.target.value)}/></label>
 {query.trim().length>=2&&<div className="aw-customer-matches">{customers.filter(c=>c.name.toLowerCase().includes(query.toLowerCase())||c.phone?.includes(query)||vehicles.some(v=>v.customerId===c.id&&v.plate.replace(/[^a-z0-9]/gi,'').toLowerCase().includes(query.replace(/[^a-z0-9]/gi,'').toLowerCase()))).slice(0,12).map(c=><button key={c.id} type="button" className="aw-person-choice" onClick={()=>{setCustomer(c);setVehicleId(vehicles.find(v=>v.customerId===c.id)?.id||'');setQuery('');setCreatingCustomer(false);setActiveOrderId(null)}}><span className="aw-person-avatar">👤</span><span><strong>{c.name}</strong><small>{vehicles.filter(v=>v.customerId===c.id).map(v=>v.plate).join(' · ')||c.phone||'Sem veículo'}</small></span><b>›</b></button>)}</div>}
 {canWrite&&<><button type="button" className="aw-new-person" onClick={()=>setCreatingCustomer(v=>!v)}>{creatingCustomer?'Fechar cadastro':'+ Cliente novo'}</button>
 {creatingCustomer&&<form className="aw-easy-form" onSubmit={e=>{e.preventDefault();void run(async()=>{const created=online?await api<{id:string}>('/customers',token,'POST',{name,phone}):await createOfflineCustomer(tenantFromToken(token),name,phone);setCustomer({id:created.id,name,phone});if(!online)onQueued();setName('');setPhone('');setCreatingCustomer(false)})}}><label>Nome do cliente<input required autoFocus placeholder="Nome" value={name} onChange={e=>setName(e.target.value)}/></label><label>Celular (opcional)<input inputMode="tel" placeholder="(11) 99999-9999" value={phone} onChange={e=>setPhone(e.target.value)}/></label><button disabled={busy||!name.trim()}>Salvar e continuar →</button></form>}</>}
 {query.trim().length<2&&!creatingCustomer&&<p className="aw-hint">Basta digitar duas letras ou parte da placa. Toque no cliente para continuar.</p>}
 </>:<><div className="aw-selected-person"><span className="aw-person-avatar">✓</span><div><small>Cliente</small><strong>{customer.name}</strong></div></div>
 <h3 className="aw-vehicle-title">2. Qual veículo?</h3>
 <div className="aw-vehicle-choices">{clientVehicles.map(v=><button type="button" key={v.id} className={'aw-vehicle-choice '+(vehicleId===v.id?'aw-vehicle-active':'')} onClick={()=>{setVehicleId(v.id);setActiveOrderId(null)}}><span>🚘</span><strong>{v.plate}</strong><small>{v.model}</small><b>{vehicleId===v.id?'✓':'Selecionar'}</b></button>)}</div>
 {canWrite&&<details className="aw-add-vehicle"><summary>+ Cadastrar outro veículo</summary><form className="aw-easy-form" onSubmit={e=>{e.preventDefault();void run(async()=>{const created=online?await api<{id:string}>('/vehicles',token,'POST',{customerId:customer.id,plate,model}):await createOfflineVehicle(tenantFromToken(token),customer.id,plate,model);setVehicleId(created.id);if(!online)onQueued();setPlate('');setModel('')})}}><label>Placa<input required autoCapitalize="characters" placeholder="ABC1D23" value={plate} onChange={e=>setPlate(e.target.value.toUpperCase())}/></label><label>Modelo<input required placeholder="Ex.: Gol branco" value={model} onChange={e=>setModel(e.target.value)}/></label><button disabled={busy}>Salvar veículo →</button></form></details>}
 </>}
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
 <section className="panel aw-orders-section">
 <div className="aw-section-title"><div><h2>Comandas</h2><p>Toque na comanda para ver os detalhes e realizar outras ações.</p></div><strong>{orders.filter(o=>o.status<4).length} em aberto</strong></div>
 <input aria-label="Pesquisar comandas" placeholder="Buscar placa, cliente ou número..." value={orderQuery} onChange={e=>setOrderQuery(e.target.value)}/>
 <div className="aw-order-filters">{[{id:'active',name:'Em aberto'},{id:'0',name:'Aguardando'},{id:'1',name:'Lavando'},{id:'2',name:'Acabamento'},{id:'3',name:'Prontas'},{id:'all',name:'Todas'}].map(f=><button key={f.id} type="button" className={orderFilter===f.id?'':'secondary'} aria-pressed={orderFilter===f.id} onClick={()=>setOrderFilter(f.id)}>{f.name}</button>)}</div>
 <div className="aw-order-grid">
 {orders.filter(o=>(orderFilter==='all'||(orderFilter==='active'?o.status<4:o.status===Number(orderFilter)))&&(()=>{
  const v=vehicles.find(v=>v.id===o.vehicleId);const cl=customers.find(c=>c.id===v?.customerId);const q=orderQuery.trim().toLowerCase();
  return !q||[o.number,v?.plate,v?.model,cl?.name].some(x=>x?.toLowerCase().includes(q));
 })()).map(o=>{
  const v=vehicles.find(v=>v.id===o.vehicleId);const cl=customers.find(c=>c.id===v?.customerId);
  const employee=employees.find(e=>e.id===o.employeeId);
  const expanded=expandedOrder===o.id;
  const statusLabel=['Aguardando','Lavando','Acabamento','Pronto','Entregue','Cancelado'][o.status]||'Desconhecido';
  return <article className="aw-order-card" key={o.id}>
   <div className="aw-order-top"><strong>Comanda #{o.number}</strong><span className={'aw-status aw-status-'+o.status}>{statusLabel}</span></div>
   <div className="aw-order-summary"><div className="aw-order-car" aria-hidden="true">🚘</div><div className="aw-order-ident"><strong>{v?.plate||'Veículo não encontrado'}</strong><span>{v?.model||'Modelo não informado'}</span><small>{cl?.name||'Cliente não encontrado'}</small></div><strong className="aw-order-price">{currency(o.total)}</strong></div>
   <div className="aw-order-employee"><span>👤 {employee?.name||'Sem responsável'}</span><button type="button" className="secondary" aria-expanded={expanded} onClick={()=>setExpandedOrder(expanded?null:o.id)}>{expanded?'Ocultar detalhes':'Ver detalhes'}</button></div>
   {expanded&&<div className="aw-order-details">
    <label>Funcionário responsável<select aria-label={'Responsável '+o.number} disabled={!canWrite||!online||o.status>=4||busy} value={o.employeeId||''} onChange={e=>void run(()=>api('/orders/'+o.id+'/employee',token,'PUT',{employeeId:e.target.value||null}))}><option value="">Sem responsável</option>{employees.filter(e=>e.isActive||e.id===o.employeeId).map(e=><option key={e.id} value={e.id}>{e.name}</option>)}</select></label>
    {canWrite&&o.status<4&&o.paymentStatus===0&&<><label>Adicionar serviço extra<div className="aw-detail-action"><select aria-label={'Serviço extra '+o.number} value={extra[o.id]||''} onChange={e=>setExtra(old=>({...old,[o.id]:e.target.value}))}><option value="">Selecione um serviço</option>{services.map(s=><option key={s.id} value={s.id}>{s.name} — {currency(s.price)}</option>)}</select><button type="button" disabled={busy||!online||!extra[o.id]} onClick={()=>void run(async()=>{await api('/orders/'+o.id+'/services',token,'POST',{serviceId:extra[o.id]});setExtra(old=>({...old,[o.id]:''}))})}>Adicionar</button></div></label>
    <label>Adicionar produto ou insumo<select aria-label={'Produto '+o.number} value={selectedProduct[o.id]||''} onChange={e=>setSelectedProduct(x=>({...x,[o.id]:e.target.value}))}><option value="">Selecione um produto</option>{products.map(p=><option key={p.id} value={p.id}>{p.name} — {currency(p.price)} · saldo {p.stock}</option>)}</select></label>
    <div className="aw-detail-action"><input aria-label={'Quantidade '+o.number} inputMode="decimal" placeholder="Quantidade" value={productQty[o.id]??'1'} onChange={e=>setProductQty(x=>({...x,[o.id]:e.target.value}))}/><button type="button" disabled={busy||!online||!selectedProduct[o.id]} onClick={()=>void run(async()=>{const quantity=Number((productQty[o.id]??'1').replace(',','.'));if(!Number.isFinite(quantity)||quantity<=0)throw new Error('Quantidade inválida');await api('/orders/'+o.id+'/products',token,'POST',{productId:selectedProduct[o.id],quantity,isConsumption:!!consume[o.id]});setSelectedProduct(x=>({...x,[o.id]:''}))})}>Adicionar</button></div>
    {canConfirmPix&&<label className="aw-check-label"><input type="checkbox" checked={!!consume[o.id]} onChange={e=>setConsume(x=>({...x,[o.id]:e.target.checked}))}/>Utilizado como insumo</label>}
    </>}
    {o.paymentStatus===2&&<p>Pagamento confirmado.</p>}
   </div>}
   {canWrite&&o.status<3&&<button type="button" className="aw-order-primary" disabled={busy} onClick={()=>void advance(o)}>{['Iniciar lavagem','Ir para acabamento','Marcar como pronto'][o.status]}</button>}
   {canWrite&&o.status===3&&<button type="button" className="aw-order-primary" disabled={!online||busy} onClick={()=>setPaymentOrderId(o.id)}>Receber pagamento</button>}
   {canWrite&&o.status<3&&o.paymentStatus===2&&<p className="aw-hint">Pagamento já confirmado.</p>}
   {canWrite&&o.status<4&&o.status!==3&&<button type="button" className="secondary aw-order-pay" disabled={!online||busy} onClick={()=>setPaymentOrderId(o.id)}>Pagamento / fechamento</button>}
  </article>;
 })}
 </div>
 {orders.filter(o=>(orderFilter==='all'||(orderFilter==='active'?o.status<4:o.status===Number(orderFilter)))&&(()=>{
 const v=vehicles.find(v=>v.id===o.vehicleId);const cl=customers.find(c=>c.id===v?.customerId);const q=orderQuery.trim().toLowerCase();return !q||[o.number,v?.plate,v?.model,cl?.name].some(x=>x?.toLowerCase().includes(q));
 })()).length===0&&<p className="aw-empty">Nenhuma comanda encontrada.</p>}
 </section></>;
}
