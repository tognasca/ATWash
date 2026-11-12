import {useEffect,useState} from 'react';
import {api,currency,type Product,type Service} from './api';
import {useDialog} from './Dialog';
type Employee={id:string;name:string;role:string;phone?:string;isActive:boolean;commissionPercent:number;userId?:string};
type Supplier={id:string;name:string;phone?:string;taxId?:string;isActive:boolean};
type Recipe={productId:string;quantity:number};
export function EmployeesAdmin({token,onError}:{token:string;onError:(s:string)=>void}){
 const dialog=useDialog();const [items,setItems]=useState<Employee[]>([]);const [editing,setEditing]=useState('');
 const [name,setName]=useState('');const [role,setRole]=useState('Lavador');const [phone,setPhone]=useState('');
 const [accessEmail,setAccessEmail]=useState('');const [accessPassword,setAccessPassword]=useState('');const [accessRole,setAccessRole]=useState(3);
 const [commission,setCommission]=useState('0');const [active,setActive]=useState(true);
 async function load(){setItems(await api<Employee[]>('/employees',token))}
 useEffect(()=>{void load().catch(e=>onError(String(e)))},[token]);
 function reset(){setEditing('');setName('');setRole('Lavador');setPhone('');setCommission('0');setActive(true)}
 async function save(){try{const commissionPercent=Number(commission.replace(',','.'));await api('/employees'+(editing?'/'+editing:''),token,editing?'PUT':'POST',{name,role,phone,isActive:active,commissionPercent});await load();reset();dialog.show('Funcionário salvo com sucesso.','success')}catch(e){onError(String(e))}}
 return <><h3>Equipe e permissões operacionais</h3><p>O cargo organiza a equipe. O acesso ao sistema continua controlado pelo perfil do usuário autenticado.</p>
 <form onSubmit={e=>{e.preventDefault();void save()}}>
 <label>Nome<input required value={name} onChange={e=>setName(e.target.value)}/></label>
 <label>Cargo<select value={role} onChange={e=>setRole(e.target.value)}><option>Lavador</option><option>Atendente</option><option>Gerente</option><option>Supervisor</option><option>Outro</option></select></label>
 <label>Telefone<input value={phone} onChange={e=>setPhone(e.target.value)}/></label>
 <label>Comissão (%)<input required inputMode="decimal" value={commission} onChange={e=>setCommission(e.target.value)}/></label>
 <label><input type="checkbox" checked={active} onChange={e=>setActive(e.target.checked)}/>Funcionário ativo</label>
 <div className="actions"><button>{editing?'Salvar':'Cadastrar funcionário'}</button>{editing&&<button type="button" className="secondary" onClick={reset}>Cancelar</button>}</div></form>
 <div className="tablewrap"><table><thead><tr><th>Funcionário</th><th>Cargo</th><th>Contato</th><th>Comissão</th><th>Status</th><th></th></tr></thead><tbody>{items.map(x=><tr key={x.id}><td>{x.name}</td><td>{x.role}</td><td>{x.phone||'—'}</td><td>{x.commissionPercent}%</td><td>{x.isActive?'Ativo':'Inativo'}</td><td><button className="secondary" onClick={()=>{setEditing(x.id);setName(x.name);setRole(x.role);setPhone(x.phone||'');setCommission(String(x.commissionPercent));setActive(x.isActive)}}>Editar</button></td></tr>)}</tbody></table></div>
 <h3>Credenciais de acesso</h3><p>Selecione um funcionário existente para criar ou redefinir seu acesso. Senha com pelo menos 12 caracteres.</p>
 <form onSubmit={e=>{e.preventDefault();void (async()=>{try{await api('/employees/'+editing+'/access',token,'POST',{email:accessEmail,password:accessPassword,role:accessRole});setAccessPassword('');dialog.show('Acesso do funcionário configurado.','success')}catch(err){onError(String(err))}})()}}>
 <label>Funcionário<select required value={editing} onChange={e=>setEditing(e.target.value)}><option value="">Selecione</option>{items.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
 <label>E-mail de login<input required type="email" value={accessEmail} onChange={e=>setAccessEmail(e.target.value)}/></label>
 <label>Senha<input required type="password" minLength={12} value={accessPassword} onChange={e=>setAccessPassword(e.target.value)}/></label>
 <label>Permissão<select value={accessRole} onChange={e=>setAccessRole(Number(e.target.value))}><option value={3}>Lavador</option><option value={2}>Atendente</option><option value={1}>Gerente</option></select></label>
 <button>Configurar acesso</button></form>
 <EmployeePerformance token={token} onError={onError}/></>
}
function EmployeePerformance({token,onError}:{token:string;onError:(s:string)=>void}){
 const [data,setData]=useState<{id:string;name:string;orders:number;revenue:number;estimatedCommission:number}[]>([]);
 async function load(){try{setData(await api<typeof data>('/employees/performance',token))}catch(e){onError(String(e))}}
 useEffect(()=>{void load()},[token]);
 return <><h3>Produtividade e comissões estimadas</h3><button className="secondary" onClick={()=>void load()}>Atualizar indicadores</button><div className="tablewrap"><table><thead><tr><th>Funcionário</th><th>Comandas entregues</th><th>Receita atribuída</th><th>Comissão estimada</th></tr></thead><tbody>{data.map(x=><tr key={x.id}><td>{x.name}</td><td>{x.orders}</td><td>{currency(x.revenue)}</td><td>{currency(x.estimatedCommission)}</td></tr>)}</tbody></table></div></>
}
export function RecipesAdmin({token,services,products,onError}:{token:string;services:Service[];products:Product[];onError:(s:string)=>void}){
 const dialog=useDialog();const [serviceId,setServiceId]=useState('');const [recipe,setRecipe]=useState<Recipe[]>([]);
 useEffect(()=>{if(!serviceId){setRecipe([]);return}void api<Recipe[]>('/services/'+serviceId+'/recipe',token).then(setRecipe).catch(e=>onError(String(e)))},[serviceId,token]);
 const supplies=products.filter(p=>p.isSupply);
 async function save(){try{await api('/services/'+serviceId+'/recipe',token,'PUT',recipe.filter(x=>x.quantity>0));dialog.show('Ficha técnica salva. O consumo será descontado ao marcar a comanda como Pronta.','success')}catch(e){onError(String(e))}}
 return <><h3>Ficha técnica por serviço</h3><p>Informe a quantidade de cada insumo consumida por lavagem, na unidade cadastrada no produto. A baixa acontece quando a comanda passa para Pronto.</p>
 <label>Serviço<select value={serviceId} onChange={e=>setServiceId(e.target.value)}><option value="">Selecione</option>{services.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
 {serviceId&&<><div className="tablewrap"><table><thead><tr><th>Insumo</th><th>Quantidade por lavagem</th></tr></thead><tbody>{supplies.map(p=><tr key={p.id}><td>{p.name} ({p.unit||'un'})</td><td><input aria-label={'Consumo '+p.name} inputMode="decimal" value={recipe.find(x=>x.productId===p.id)?.quantity??0} onChange={e=>{const q=Number(e.target.value.replace(',','.'));setRecipe(old=>[...old.filter(x=>x.productId!==p.id),{productId:p.id,quantity:Number.isFinite(q)?q:0}])}}/></td></tr>)}</tbody></table></div><button onClick={()=>void save()}>Salvar ficha técnica</button></>}</>
}
export function SuppliersAdmin({token,onError}:{token:string;onError:(s:string)=>void}){
 const dialog=useDialog();const [items,setItems]=useState<Supplier[]>([]);const [id,setId]=useState('');
 const [name,setName]=useState('');const [phone,setPhone]=useState('');const [taxId,setTaxId]=useState('');const [active,setActive]=useState(true);
 async function load(){setItems(await api<Supplier[]>('/suppliers',token))}
 useEffect(()=>{void load().catch(e=>onError(String(e)))},[token]);
 async function save(){try{await api('/suppliers'+(id?'/'+id:''),token,id?'PUT':'POST',{name,phone,taxId,isActive:active});await load();setId('');setName('');setPhone('');setTaxId('');setActive(true);dialog.show('Fornecedor salvo.','success')}catch(e){onError(String(e))}}
 return <><h3>Fornecedores</h3><form onSubmit={e=>{e.preventDefault();void save()}}>
 <label>Nome<input required value={name} onChange={e=>setName(e.target.value)}/></label><label>Telefone<input value={phone} onChange={e=>setPhone(e.target.value)}/></label><label>CPF/CNPJ<input value={taxId} onChange={e=>setTaxId(e.target.value)}/></label>
 <label><input type="checkbox" checked={active} onChange={e=>setActive(e.target.checked)}/>Ativo</label><button>{id?'Salvar':'Cadastrar fornecedor'}</button></form>
 <div className="tablewrap"><table><thead><tr><th>Nome</th><th>Telefone</th><th>Status</th><th></th></tr></thead><tbody>{items.map(x=><tr key={x.id}><td>{x.name}</td><td>{x.phone}</td><td>{x.isActive?'Ativo':'Inativo'}</td><td><button className="secondary" onClick={()=>{setId(x.id);setName(x.name);setPhone(x.phone||'');setTaxId(x.taxId||'');setActive(x.isActive)}}>Editar</button></td></tr>)}</tbody></table></div></>
}
