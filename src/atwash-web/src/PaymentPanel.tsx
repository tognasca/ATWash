import {useEffect,useState} from 'react';
import {api,currency,type Order} from './api';
type Method=0|1|2;
type Ledger={total:number;paid:number;remaining:number;payments:{id:string;method:Method;amount:number;confirmedAt:string;reference?:string}[]};
type Props={order:Order;token:string;onClose:()=>void;onChanged:()=>Promise<void>;onError:(error:string)=>void;canConfirmPix:boolean};
export default function PaymentPanel({order,token,onClose,onChanged,onError,canConfirmPix}:Props){
 const [ledger,setLedger]=useState<Ledger|null>(null);
 const [method,setMethod]=useState<Method>(0);
 const [amount,setAmount]=useState('');
 const [tendered,setTendered]=useState('');
 const [reference,setReference]=useState('');
 const [busy,setBusy]=useState(false);
 const [operationId,setOperationId]=useState(()=>crypto.randomUUID());
 async function load(){const result=await api<Ledger>('/orders/'+order.id+'/payments',token);setLedger(result);setAmount(result.remaining.toFixed(2));setTendered(result.remaining.toFixed(2))}
 useEffect(()=>{void load().catch(e=>onError(String(e)))},[order.id,token]);
 const numeric=(s:string)=>Number(s.replace(',','.'));
 const paymentAmount=numeric(amount),given=numeric(tendered);
 async function confirm(){
  if(!ledger)return;
  if(!Number.isFinite(paymentAmount)||paymentAmount<=0||paymentAmount>ledger.remaining){onError('Informe um valor até o saldo pendente.');return}
  if(method===0&&(!Number.isFinite(given)||given<paymentAmount)){onError('Valor recebido insuficiente.');return}
  if(method===1&&!reference.trim()){onError('Informe o NSU/autorização da maquininha.');return}
  if(method===2&&!reference.trim()){onError('Informe a referência do Pix recebido.');return}
  if(!window.confirm('Confirma que o valor foi realmente recebido? Esta operação será registrada.'))return;
  setBusy(true);onError('');
  try{
   await api('/orders/'+order.id+'/payments',token,'POST',{operationId,method,amount:paymentAmount,tendered:method===0?given:paymentAmount,reference:reference.trim()||null});
   setOperationId(crypto.randomUUID());setReference('');await load();await onChanged();
  }catch(e){onError(String(e))}finally{setBusy(false)}
 }
 async function deliver(){
  if(!window.confirm('Confirmar a entrega do veículo e fechar a comanda?'))return;
  setBusy(true);onError('');
  try{await api('/orders/'+order.id+'/deliver',token,'POST');await onChanged();onClose()}
  catch(e){onError(String(e))}finally{setBusy(false)}
 }
 return <section className="panel payment-panel"><div className="actions"><h2>Pagamento · Comanda #{order.number}</h2><button className="secondary" onClick={onClose}>Fechar</button></div>
 <div className="payment-summary"><span>Total: <strong>{currency(ledger?.total??order.total)}</strong></span><span>Recebido: <strong>{currency(ledger?.paid??0)}</strong></span><span>Falta: <strong>{currency(ledger?.remaining??order.total)}</strong></span></div>
 {ledger&&ledger.remaining>0&&<form onSubmit={e=>{e.preventDefault();void confirm()}}>
 <label>Forma de pagamento<select value={method} onChange={e=>{setMethod(Number(e.target.value) as Method);setReference('')}}><option value={0}>Dinheiro</option><option value={1}>Cartão (maquininha externa)</option>{canConfirmPix&&<option value={2}>Pix já recebido (confirmação manual)</option>}</select></label>
 <label>Valor a registrar (R$)<input inputMode="decimal" required value={amount} onChange={e=>setAmount(e.target.value)}/></label>
 {method===0&&<><label>Dinheiro entregue pelo cliente (R$)<input inputMode="decimal" required value={tendered} onChange={e=>setTendered(e.target.value)}/></label><strong>Troco: {currency(Math.max(0,(Number.isFinite(given)?given:0)-(Number.isFinite(paymentAmount)?paymentAmount:0)))}</strong></>}
 {method!==0&&<label>{method===1?'NSU / autorização da maquininha':'Referência da transferência Pix'}<input required value={reference} onChange={e=>setReference(e.target.value)} placeholder="Confirme o recebimento antes de registrar"/></label>}
 {method===2&&<p>Este sistema não consulta o banco nem confirma Pix automaticamente. Somente registre após verificar o crédito.</p>}
 <button disabled={busy}>Confirmar recebimento</button></form>}
 {ledger&&<><h3>Pagamentos registrados</h3>{ledger.payments.length===0?<p>Nenhum pagamento registrado.</p>:<ul>{ledger.payments.map(p=><li key={p.id}>{['Dinheiro','Cartão','Pix'][p.method]} · {currency(p.amount)} · {new Date(p.confirmedAt).toLocaleString('pt-BR')}</li>)}</ul>}
 {ledger.remaining===0&&order.status===3&&<button disabled={busy} onClick={()=>void deliver()}>Entregar veículo e fechar comanda</button>}
 {ledger.remaining===0&&order.status!==3&&<p>Pagamento completo. Aguarde a lavagem ficar pronta para entregar.</p>}</>}
 </section>;
}
