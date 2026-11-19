import {useEffect,useState,type FormEvent} from 'react';
import {api,defaultFlexSettings,type FlexSettings,type Service} from './api';
import {useDialog} from './Dialog';

type Props={token:string;services:Service[];reload:()=>Promise<void>;onError:(message:string)=>void};

export default function FlexSettingsAdmin({token,services,reload,onError}:Props){
 const dialog=useDialog();
 const [settings,setSettings]=useState<FlexSettings|null>(null);
 const [busy,setBusy]=useState(false);

 useEffect(()=>{
  let active=true;
  void api<FlexSettings>('/flex/settings',token)
   .then(value=>{if(active)setSettings(value)})
   .catch(error=>{if(active)onError(String(error))});
  return ()=>{active=false};
 },[token]);

 async function save(event:FormEvent){
  event.preventDefault();
  setBusy(true);
  try{
   setSettings(await api<FlexSettings>('/flex/settings',token,'PUT',settings));
   await reload();
   dialog.show('Configuração salva.','success');
  }catch(error){onError(String(error))}
  finally{setBusy(false)}
 }

 if(!settings)return <p>Configurações da operação indisponíveis.</p>;
 return <form onSubmit={save}>
  <fieldset disabled={busy}>
   <legend>Nomes das etapas de lavagem</legend>
   {settings.stageNames.map((name,index)=><label key={index}>{defaultFlexSettings.stageNames[index]}
    <input required maxLength={40} value={name} onChange={event=>{
     const value=event.target.value;
     setSettings({...settings,stageNames:settings.stageNames.map((old,i)=>i===index?value:old)});
    }}/>
   </label>)}
  </fieldset>
  <h3>Pacotes de serviços</h3>
  <p>Escolha os serviços que entram juntos na comanda. O preço do pacote soma os preços desses serviços para a categoria escolhida.</p>
  {settings.packages.map((item,index)=><fieldset key={index} disabled={busy}>
   <legend>Pacote {index+1}</legend>
   <label>Nome<input required maxLength={120} value={item.name} onChange={event=>{
    const name=event.target.value;
    setSettings({...settings,packages:settings.packages.map((old,i)=>i===index?{...old,name}:old)});
   }}/></label>
   <label>Serviços<select multiple value={item.serviceIds} onChange={event=>{
    const serviceIds=Array.from(event.target.selectedOptions,option=>option.value);
    setSettings({...settings,packages:settings.packages.map((old,i)=>i===index?{...old,serviceIds}:old)});
   }}>{services.map(service=><option key={service.id} value={service.id}>{service.name}</option>)}</select></label>
   {item.serviceIds.some(id=>!services.some(service=>service.id===id))&&<p>Selecione novamente os serviços deste pacote. Um item saiu do catálogo.</p>}
   <button className="secondary" type="button" onClick={()=>setSettings({...settings,packages:settings.packages.filter((_,i)=>i!==index)})}>Remover pacote</button>
  </fieldset>)}
  <div className="actions">
   <button className="secondary" type="button" disabled={busy||settings.packages.length>=50}
    onClick={()=>setSettings({...settings,packages:[...settings.packages,{name:'',serviceIds:[]}]})}>Adicionar pacote</button>
   <button disabled={busy}>Salvar configuração</button>
  </div>
 </form>;
}
