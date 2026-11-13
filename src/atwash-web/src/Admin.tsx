import {useEffect,useState} from 'react';
import {useDialog} from './Dialog';
import FinanceAdmin from './FinanceAdmin';
import {EmployeesAdmin,RecipesAdmin,SuppliersAdmin} from './OperationsAdmin';
import SettingsAdmin from './SettingsAdmin';
import {api,ApiError,currency,type Product,type Service} from './api';
type Props={token:string;role:string;services:Service[];products:Product[];reload:()=>Promise<void>;onError:(message:string)=>void};
type Tab='services'|'products'|'employees'|'suppliers'|'recipes'|'finance'|'settings';
export default function Admin({token,role,services,products,reload,onError}:Props){
 const dialog=useDialog();
 const [tab,setTab]=useState<Tab>('services');
 const [name,setName]=useState('');const [price,setPrice]=useState('');
 const [minStock,setMinStock]=useState('0');const [avgCost,setAvgCost]=useState('0');const [unit,setUnit]=useState('un');const [isSupply,setIsSupply]=useState(false);const [supplierId,setSupplierId]=useState('');
 const [suppliers,setSuppliers]=useState<{id:string;name:string}[]>([]);
 useEffect(()=>{void api<typeof suppliers>('/suppliers',token).then(setSuppliers).catch(()=>{})},[token,tab]);
 const [stock,setStock]=useState('0');const [editing,setEditing]=useState<string|null>(null);
 const [busy,setBusy]=useState(false);
 const [movements,setMovements]=useState<{id:string;productId:string;kind:string;quantity:number;balanceAfter:number;reason:string;createdAt:string}[]>([]);
 const [adjustId,setAdjustId]=useState('');const [adjustQty,setAdjustQty]=useState('');const [adjustReason,setAdjustReason]=useState('');const [purchaseCost,setPurchaseCost]=useState('');
 useEffect(()=>{if(tab==='products')void api<typeof movements>('/inventory/movements',token).then(setMovements).catch(e=>onError(String(e)))},[tab,token,products]);
 async function adjust(){await run(async()=>{
  const quantity=Number(adjustQty.replace(',','.'));
  if(!adjustId||!Number.isFinite(quantity)||quantity===0||!adjustReason.trim())throw new Error('Informe produto, quantidade e motivo.');
  await api('/products/'+adjustId+'/stock',token,'POST',{quantity,reason:adjustReason,unitCost:quantity>0&&purchaseCost.trim()?Number(purchaseCost.replace(',','.')):null});
  setAdjustQty('');setAdjustReason('');setPurchaseCost('');
 })}
 const tabs:[Tab,string][]=[['services','Serviços'],['products','Produtos e estoque'],['employees','Funcionários'],['suppliers','Fornecedores'],['recipes','Fichas técnicas'],['finance','Financeiro'],['settings','Configurações']];
 function clear(){setEditing(null);setName('');setPrice('');setStock('0');setMinStock('0');setAvgCost('0');setUnit('un');setIsSupply(false);setSupplierId('')}
 async function run(action:()=>Promise<unknown>){setBusy(true);onError('');try{await action();await reload();dialog.show('Operação realizada com sucesso.','success')}catch(e){onError(e instanceof ApiError?e.message:String(e))}finally{setBusy(false)}}
 const rows=tab==='services'?services:products;
 return <div className="admin-layout"><nav className="admin-menu" aria-label="Submenu administrativo">{tabs.map(([id,label])=><button key={id} className={tab===id?'active':'secondary'} onClick={()=>{setTab(id);clear()}}>{label}</button>)}</nav>
 <section className="panel admin-content"><h2>{tabs.find(([id])=>id===tab)?.[1]}</h2>
 {(tab==='services'||tab==='products')?<><form onSubmit={e=>{e.preventDefault();void run(async()=>{const value=Number(price.replace(',','.'));const quantity=Number(stock.replace(',','.'));if(!Number.isFinite(value)||value<0||!Number.isFinite(quantity)||quantity<0)throw new Error('Valores inválidos');const base=tab==='services'?'/services':'/products';const saved=await api<{id:string}>(base+(editing?'/'+editing:''),token,editing?'PUT':'POST',tab==='services'?{name,price:value}:{name,price:value,stock:editing?(products.find(p=>p.id===editing)?.stock??0):quantity});
 if(tab==='products')await api('/products/'+(editing||saved.id)+'/details',token,'PUT',{name,price:value,minimumStock:Number(minStock.replace(',','.')),averageCost:Number(avgCost.replace(',','.')),unit,isSupply,supplierId:supplierId||null});
 clear()})}}>
 <label>Nome<input required maxLength={160} value={name} onChange={e=>setName(e.target.value)}/></label>
 <label>Preço (R$)<input required inputMode="decimal" value={price} onChange={e=>setPrice(e.target.value)}/></label>
 {tab==='products'&&<><label>Tipo<select value={isSupply?'supply':'resale'} onChange={e=>setIsSupply(e.target.value==='supply')}><option value="resale">Revenda</option><option value="supply">Insumo interno</option></select></label>
 <label>Unidade<select value={unit} onChange={e=>setUnit(e.target.value)}><option value="un">Unidade</option><option value="ml">Mililitro</option><option value="l">Litro</option><option value="g">Grama</option><option value="kg">Quilo</option></select></label>
 <label>Estoque mínimo<input inputMode="decimal" value={minStock} onChange={e=>setMinStock(e.target.value)}/></label>
 <label>Custo médio (R$)<input inputMode="decimal" value={avgCost} onChange={e=>setAvgCost(e.target.value)}/></label>
 <label>Fornecedor<select value={supplierId} onChange={e=>setSupplierId(e.target.value)}><option value="">Sem fornecedor</option>{suppliers.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label></>}
 {tab==='products'&&<label>Estoque inicial (novos produtos)<input required inputMode="decimal" disabled={!!editing} value={stock} onChange={e=>setStock(e.target.value)}/></label>}
 <div className="actions"><button disabled={busy}>{editing?'Salvar alterações':'Cadastrar'}</button>{editing&&<button type="button" className="secondary" onClick={clear}>Cancelar</button>}</div></form>
 <div className="tablewrap catalog-cards"><table><thead><tr><th>Nome</th><th>Preço</th>{tab==='products'&&<th>Estoque</th>}<th>Ações</th></tr></thead><tbody>{rows.map(item=><tr key={item.id}><td>{item.name}</td><td>{currency(item.price)}</td>{tab==='products'&&<td>{(item as Product).stock}</td>}<td><button className="secondary" onClick={()=>{setEditing(item.id);setName(item.name);setPrice(String(item.price));setStock(String((item as Product).stock??0));setMinStock(String((item as Product).minimumStock??0));setAvgCost(String((item as Product).averageCost??0));setUnit((item as Product).unit||'un');setIsSupply(!!(item as Product).isSupply);setSupplierId((item as Product).supplierId||'')}}>Editar</button> {tab==='services'&&<button className="secondary" disabled={busy} onClick={()=>{void (async()=>{if(await dialog.confirm('Deseja realmente excluir este serviço?'))await run(()=>api('/services/'+item.id,token,'DELETE'))})()}}>Excluir</button>}</td></tr>)}</tbody></table></div>
 {tab==='products'&&<><h3>Movimentação de estoque</h3><form onSubmit={e=>{e.preventDefault();void adjust()}}>
 <label>Produto<select required value={adjustId} onChange={e=>setAdjustId(e.target.value)}><option value="">Selecione</option>{products.map(p=><option key={p.id} value={p.id}>{p.name} — saldo {p.stock}</option>)}</select></label>
 <label>Quantidade (+ entrada / − saída)<input required inputMode="decimal" value={adjustQty} onChange={e=>setAdjustQty(e.target.value)}/></label>
 <label>Custo unitário da compra (R$, opcional para entradas)<input inputMode="decimal" value={purchaseCost} onChange={e=>setPurchaseCost(e.target.value)}/></label><label>Motivo<input required maxLength={300} value={adjustReason} onChange={e=>setAdjustReason(e.target.value)}/></label>
 <button disabled={busy}>Registrar movimentação</button></form>
 <h3>Histórico de estoque</h3><div className="tablewrap"><table><thead><tr><th>Data</th><th>Produto</th><th>Tipo</th><th>Quantidade</th><th>Saldo</th><th>Motivo</th></tr></thead><tbody>{movements.map(m=><tr key={m.id}><td>{new Date(m.createdAt).toLocaleString('pt-BR')}</td><td>{products.find(p=>p.id===m.productId)?.name??m.productId}</td><td>{m.kind}</td><td>{m.quantity}</td><td>{m.balanceAfter}</td><td>{m.reason}</td></tr>)}</tbody></table></div></>}
 </>:tab==='employees'?<EmployeesAdmin token={token} onError={onError}/>:tab==='suppliers'?<SuppliersAdmin token={token} onError={onError}/>:tab==='recipes'?<RecipesAdmin token={token} services={services} products={products} onError={onError}/>:tab==='finance'?<FinanceAdmin token={token} onError={onError}/>:tab==='settings'?<SettingsAdmin token={token} role={role} onError={onError}/>:null}</section></div>
}
