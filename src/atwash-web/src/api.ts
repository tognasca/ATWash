export type Customer={id:string;name:string;phone?:string;vehicles?:Vehicle[]};
export type Vehicle={id:string;customerId:string;plate:string;model:string;brand?:string};
export type Service={id:string;name:string;price:number};
export type Product={id:string;name:string;price:number;stock:number};
export type Order={id:string;number:string;status:number;paymentStatus:number;total:number;vehicleId:string};
export const currency=(n:number)=>new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(n);
export async function api<T>(path:string,token:string,method='GET',body?:unknown):Promise<T>{
 const response=await fetch('/api'+path,{method,headers:{...(token?{Authorization:'Bearer '+token}:{}),'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
 if(!response.ok){
  const message=(await response.text()).replace(/^"|"$/g,'');
  throw new Error(message||'Falha HTTP '+response.status);
 }
 if(response.status===204)return undefined as T;
 return response.json() as Promise<T>;
}
