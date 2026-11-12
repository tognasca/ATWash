import {useEffect,useState} from 'react';
import {useDialog} from './Dialog';
import {api,currency} from './api';
type Cash={open:boolean;session?:{id:string;openingFloat:number;openedAt:string};expectedCash?:number;cashSales?:number;cashRefunds?:number;movements?:{id:string;kind:number;amount:number;reason:string}[]};
type Props={token:string;onError:(message:string)=>void};
const number=(value:string)=>Number(value.replace(',','.'));
export default function FinanceAdmin({token,onError}:Props){
 const dialog=useDialog();
 const [cash,setCash]=useState<Cash|null>(null);
 const [payments,setPayments]=useState<{id:string;orderNumber:string;method:number;amount:number;refunded:number;reconciled:boolean}[]>([]);const [amount,setAmount]=useState('0');const [note,setNote]=useState('');
 const [kind,setKind]=useState(0);const [busy,setBusy]=useState(false);
 const [paymentId,setPaymentId]=useState('');const [bankReference,setBankReference]=useState('');
 const [refundAmount,setRefundAmount]=useState('');const [refundReason,setRefundReason]=useState('');
 const [refundReference,setRefundReference]=useState('');
 async function load(){const [cashResult,paymentResult]=await Promise.all([api<Cash>('/cash/current',token),api<typeof payments>('/finance/payments',token)]);setCash(cashResult);setPayments(paymentResult)}
 useEffect(()=>{void load().catch(e=>onError(String(e)))},[token]);
 async function run(fn:()=>Promise<unknown>){setBusy(true);onError('');try{await fn();await load();setNote('');setAmount('0');dialog.show('Operação financeira registrada com sucesso.','success')}catch(e){onError(String(e))}finally{setBusy(false)}}
 return <div className="finance-sections"><h3>Controle de caixa</h3>
 {cash?.open?<><p>Caixa aberto desde {new Date(cash.session!.openedAt).toLocaleString('pt-BR')}</p>
 <div className="payment-summary"><span>Fundo: <strong>{currency(cash.session!.openingFloat)}</strong></span><span>Dinheiro recebido: <strong>{currency(cash.cashSales||0)}</strong></span><span>Saldo esperado: <strong>{currency(cash.expectedCash||0)}</strong></span></div>
 <form onSubmit={e=>{e.preventDefault();void run(()=>api('/cash/movements',token,'POST',{kind,amount:number(amount),reason:note}))}}>
 <h3>Suprimento ou retirada</h3><label>Operação<select value={kind} onChange={e=>setKind(Number(e.target.value))}><option value={0}>Suprimento</option><option value={1}>Retirada</option></select></label>
 <label>Valor<input required inputMode="decimal" value={amount} onChange={e=>setAmount(e.target.value)}/></label>
 <label>Motivo<input required value={note} onChange={e=>setNote(e.target.value)}/></label><button disabled={busy}>Registrar movimentação</button></form>
 <h3>Conferência e fechamento</h3><form onSubmit={e=>{e.preventDefault();void (async()=>{if(await dialog.confirm('Fechar o caixa com o valor contado informado?'))await run(()=>api('/cash/close',token,'POST',{countedCash:number(amount),note}))})()}}>
 <label>Dinheiro contado (R$)<input required inputMode="decimal" value={amount} onChange={e=>setAmount(e.target.value)}/></label><label>Observação<input value={note} onChange={e=>setNote(e.target.value)}/></label><button disabled={busy}>Fechar caixa</button></form>
 </>:<form onSubmit={e=>{e.preventDefault();void run(()=>api('/cash/open',token,'POST',{openingFloat:number(amount)}))}}><p>Nenhum caixa aberto.</p><label>Fundo inicial (R$)<input required inputMode="decimal" value={amount} onChange={e=>setAmount(e.target.value)}/></label><button disabled={busy}>Abrir caixa</button></form>}
 <hr/><h3>Pagamentos registrados</h3><div className="tablewrap"><table><thead><tr><th>Comanda</th><th>Forma</th><th>Valor</th><th>Estornado</th><th>Conciliação</th><th></th></tr></thead><tbody>{payments.map(p=><tr key={p.id}><td>#{p.orderNumber}</td><td>{['Dinheiro','Cartão','Pix'][p.method]}</td><td>{currency(p.amount)}</td><td>{currency(p.refunded)}</td><td>{p.reconciled?'Conciliado':'Pendente'}</td><td><button className="secondary" onClick={()=>setPaymentId(p.id)}>Selecionar</button></td></tr>)}</tbody></table></div>
 <hr/><h3>Conciliação bancária manual</h3><p>Confira a transação no extrato antes de vincular. Isto não consulta o banco automaticamente.</p>
 <form onSubmit={e=>{e.preventDefault();void run(()=>api('/payments/'+paymentId+'/reconcile',token,'POST',{bankReference}))}}>
 <label>ID do pagamento<input required value={paymentId} onChange={e=>setPaymentId(e.target.value)}/></label><label>Identificador do extrato<input required value={bankReference} onChange={e=>setBankReference(e.target.value)}/></label><button disabled={busy}>Conciliar pagamento</button></form>
 <hr/><h3>Estorno confirmado externamente</h3><p>Primeiro efetue o estorno na maquininha ou banco. Depois registre a confirmação aqui.</p>
 <form onSubmit={e=>{e.preventDefault();void (async()=>{if(await dialog.confirm('Você já devolveu este valor ao cliente?'))await run(()=>api('/payments/'+paymentId+'/refund',token,'POST',{amount:number(refundAmount),reason:refundReason,externalReference:refundReference}))})()}}>
 <label>ID do pagamento<input required value={paymentId} onChange={e=>setPaymentId(e.target.value)}/></label><label>Valor devolvido<input required inputMode="decimal" value={refundAmount} onChange={e=>setRefundAmount(e.target.value)}/></label><label>Motivo<input required value={refundReason} onChange={e=>setRefundReason(e.target.value)}/></label><label>Comprovante / referência externa<input required value={refundReference} onChange={e=>setRefundReference(e.target.value)}/></label><button disabled={busy}>Registrar estorno realizado</button></form></div>;
}
