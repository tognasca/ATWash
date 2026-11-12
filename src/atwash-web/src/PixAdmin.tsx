import {useEffect,useState} from 'react';
import {api} from './api';
type Settings={pixKey:string;merchantName:string;merchantCity:string};
export default function PixAdmin({token,onError}:{token:string;onError:(error:string)=>void}){
 const [pixKey,setPixKey]=useState('');const [merchantName,setMerchantName]=useState('');
 const [merchantCity,setMerchantCity]=useState('');const [busy,setBusy]=useState(false);
 useEffect(()=>{void api<Settings>('/settings/pix',token).then(s=>{setPixKey(s.pixKey);setMerchantName(s.merchantName);setMerchantCity(s.merchantCity)}).catch(e=>{if(!String(e).includes('404'))onError(String(e))})},[token]);
 async function save(){setBusy(true);onError('');try{await api('/settings/pix',token,'PUT',{pixKey,merchantName,merchantCity});window.alert('Configuração Pix salva.')}catch(e){onError(String(e))}finally{setBusy(false)}}
 return <form onSubmit={e=>{e.preventDefault();void save()}}><p>Chave Pix do estabelecimento para gerar cobrança estática com valor. Não há confirmação bancária automática.</p>
 <label>Chave Pix<input required maxLength={77} value={pixKey} onChange={e=>setPixKey(e.target.value)}/></label>
 <label>Nome do recebedor<input required maxLength={25} value={merchantName} onChange={e=>setMerchantName(e.target.value)}/></label>
 <label>Cidade<input required maxLength={15} value={merchantCity} onChange={e=>setMerchantCity(e.target.value)}/></label>
 <button disabled={busy}>Salvar configuração Pix</button></form>;
}
