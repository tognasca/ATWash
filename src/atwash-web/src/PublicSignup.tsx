import {useEffect,useState,type FormEvent} from 'react';
import {api,ApiError,currency} from './api';
type Plan={id:string;name:string;monthlyPrice:number;maxDevices:number};
export default function PublicSignup(){
 const [plans,setPlans]=useState<Plan[]>([]);
 const [selected,setSelected]=useState('');
 const [company,setCompany]=useState('');
 const [email,setEmail]=useState('');
 const [password,setPassword]=useState('');
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 useEffect(()=>{void api<Plan[]>('/public/plans','').then(p=>{setPlans(p);setSelected(p.find(x=>x.name==='Profissional')?.id??p[0]?.id??'')}).catch(()=>setError('Não foi possível carregar os planos.'))},[]);
 async function submit(e:FormEvent){
  e.preventDefault();setBusy(true);setError('');
  try{
   const result=await api<{checkoutUrl:string}>('/public/register','','POST',{companyName:company,email,password,planId:selected});
   const url=new URL(result.checkoutUrl);
   if(url.protocol!=='https:'||!(url.hostname==='mercadopago.com.br'||url.hostname.endsWith('.mercadopago.com.br')))
    throw new Error('Endereço de pagamento inválido.');
   window.location.assign(url.toString());
  }catch(e){setError(e instanceof ApiError?(e.status===429?'Muitas tentativas. Aguarde um minuto.':e.message):String(e))}
  finally{setBusy(false)}
 }
 return <main className="aw-public-signup"><header className="aw-signup-head"><a href="/">ATWash<span>Control</span></a><a href="/">Já tenho conta · Entrar</a></header><section className="aw-signup-intro"><span>GESTÃO PROFISSIONAL DE LAVA-RÁPIDO</span><h1>Escolha o plano ideal para sua operação</h1><p>Sem período gratuito. Sua empresa será liberada somente após a confirmação da assinatura pelo Mercado Pago.</p></section>
 <div className="aw-signup-plans">{plans.map(p=><button type="button" key={p.id} className={'aw-signup-plan '+(selected===p.id?'selected':'')} onClick={()=>setSelected(p.id)}><small>{p.name}</small><strong>{currency(p.monthlyPrice)}<span>/mês</span></strong><span>Até {p.maxDevices} dispositivo(s) ativo(s)</span><b>{selected===p.id?'✓ Selecionado':'Selecionar plano'}</b></button>)}</div>
 <section className="aw-signup-form"><div><h2>Cadastre seu lava-rápido</h2><p>Após o cadastro, você será direcionado ao pagamento seguro do Mercado Pago.</p></div><form onSubmit={submit}><label>Nome da empresa<input required maxLength={160} value={company} onChange={e=>setCompany(e.target.value)} autoComplete="organization"/></label><label>E-mail do proprietário<input type="email" required maxLength={254} value={email} onChange={e=>setEmail(e.target.value)} autoComplete="email"/></label><label>Crie uma senha (mínimo 12 caracteres)<input type="password" required minLength={12} maxLength={128} value={password} onChange={e=>setPassword(e.target.value)} autoComplete="new-password"/></label>{error&&<p className="aw-signup-error" role="alert">{error}</p>}<button disabled={busy||!selected}>{busy?'Preparando assinatura...':'Continuar para pagamento seguro'}</button><small>Ao continuar, você inicia uma assinatura mensal. O acesso será liberado após a confirmação do pagamento.</small></form></section></main>;
}
