import {useEffect,useState,type FormEvent} from 'react';
import {api,type Branch} from './api';

export default function BranchAdmin({token,reload,onError}:{token:string;reload:()=>Promise<void>;onError:(message:string)=>void}){
 const [branches,setBranches]=useState<Branch[]>([]);
 const [editing,setEditing]=useState<string|null>(null);
 const [name,setName]=useState('');
 const [busy,setBusy]=useState(false);

 useEffect(()=>{
  let active=true;
  void api<Branch[]>('/branches',token)
   .then(items=>{if(active)setBranches(items)})
   .catch(error=>{if(active)onError(String(error))});
  return ()=>{active=false};
 },[token]);

 async function save(event:FormEvent){
  event.preventDefault();
  setBusy(true);
  try{
   const branch=await api<Branch>(editing?'/branches/'+editing:'/branches',token,editing?'PUT':'POST',{name});
   setBranches(items=>[...items.filter(item=>item.id!==branch.id),branch].sort((a,b)=>a.name.localeCompare(b.name)));
   setEditing(null);
   setName('');
   await reload();
  }catch(error){onError(String(error))}
  finally{setBusy(false)}
 }

 return <section>
  <h3>Unidades</h3>
  <form onSubmit={save}>
   <label>Nome da unidade<input required maxLength={120} value={name} onChange={event=>setName(event.target.value)}/></label>
   <button disabled={busy||!name.trim()}>{editing?'Salvar nome':'Cadastrar unidade'}</button>
   {editing&&<button type="button" className="secondary" disabled={busy} onClick={()=>{setEditing(null);setName('')}}>Cancelar edição</button>}
  </form>
  <ul>{branches.map(branch=><li key={branch.id}>
   {branch.name} <button type="button" className="secondary" disabled={busy} onClick={()=>{setEditing(branch.id);setName(branch.name)}}>Editar</button>
  </li>)}</ul>
 </section>;
}
