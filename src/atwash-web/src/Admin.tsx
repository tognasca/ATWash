import {useState} from 'react';
import FinanceAdmin from './FinanceAdmin';
import PixAdmin from './PixAdmin';
import {api,currency,type Product,type Service} from './api';
type Props={token:string;services:Service[];products:Product[];reload:()=>Promise<void>;onError:(message:string)=>void};
type Tab='services'|'products'|'employees'|'finance'|'settings';
export default function Admin({token,services,products,reload,onError}:Props){
 const [tab,setTab]=useState<Tab>('services');
 const [name,setName]=useState('');const [price,setPrice]=useState('');
 const [stock,setStock]=useState('0');const [editing,setEditing]=useState<string|null>(null);
 const [busy,setBusy]=useState(false);
 const tabs:[Tab,string][]=[['services','Serviços'],['products','Produtos e estoque'],['employees','Funcionários'],['finance','Financeiro'],['settings','Configurações']];
 function clear(){setEditing(null);setName('');setPrice('');setStock('0')}
 async function run(action:()=>Promise<unknown>){setBusy(true);onError('');try{await action();await reload()}catch(e){onError(String(e))}finally{setBusy(false)}}
 const rows=tab==='services'?services:products;
 return <div className="admin-layout"><nav className="admin-menu" aria-label="Submenu administrativo">{tabs.map(([id,label])=><button key={id} className={tab===id?'active':'secondary'} onClick={()=>{setTab(id);clear()}}>{label}</button>)}</nav>
 <section className="panel admin-content"><h2>{tabs.find(([id])=>id===tab)?.[1]}</h2>
 {(tab==='services'||tab==='products')?<><form onSubmit={e=>{e.preventDefault();void run(async()=>{const value=Number(price.replace(',','.'));const quantity=Number(stock.replace(',','.'));if(!Number.isFinite(value)||value<0||!Number.isFinite(quantity)||quantity<0)throw new Error('Valores inválidos');const base=tab==='services'?'/services':'/products';await api(base+(editing?'/'+editing:''),token,editing?'PUT':'POST',tab==='services'?{name,price:value}:{name,price:value,stock:quantity});clear()})}}>
 <label>Nome<input required maxLength={160} value={name} onChange={e=>setName(e.target.value)}/></label>
 <label>Preço (R$)<input required inputMode="decimal" value={price} onChange={e=>setPrice(e.target.value)}/></label>
 {tab==='products'&&<label>Estoque<input required inputMode="decimal" value={stock} onChange={e=>setStock(e.target.value)}/></label>}
 <div className="actions"><button disabled={busy}>{editing?'Salvar alterações':'Cadastrar'}</button>{editing&&<button type="button" className="secondary" onClick={clear}>Cancelar</button>}</div></form>
 <div className="tablewrap"><table><thead><tr><th>Nome</th><th>Preço</th>{tab==='products'&&<th>Estoque</th>}<th>Ações</th></tr></thead><tbody>{rows.map(item=><tr key={item.id}><td>{item.name}</td><td>{currency(item.price)}</td>{tab==='products'&&<td>{(item as Product).stock}</td>}<td><button className="secondary" onClick={()=>{setEditing(item.id);setName(item.name);setPrice(String(item.price));setStock(String((item as Product).stock??0))}}>Editar</button> {tab==='services'&&<button className="secondary" disabled={busy} onClick={()=>{if(!window.confirm('Excluir serviço?'))return;void run(()=>api('/services/'+item.id,token,'DELETE'))}}>Excluir</button>}</td></tr>)}</tbody></table></div></>:<p>Este módulo administrativo ainda está em desenvolvimento. Nenhuma configuração fictícia será exibida.</p>}</section></div>
}
