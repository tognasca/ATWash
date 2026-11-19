export type Customer={id:string;name:string;phone?:string;vehicles?:Vehicle[]};
export type Vehicle={id:string;customerId:string;plate:string;model:string;brand?:string};
export const vehicleCategories=[
 {id:1,key:'Motorcycle',label:'Moto'},
 {id:2,key:'PassengerCar',label:'Carro de passeio'},
 {id:3,key:'Suv',label:'SUV'},
 {id:4,key:'Pickup',label:'Picape'},
 {id:5,key:'Van',label:'Van'}
] as const;
export type CategoryPrices=Partial<Record<(typeof vehicleCategories)[number]['key'],number>>;
export type Service={id:string;name:string;price:number;categoryPrices?:CategoryPrices|null};
export function servicePrice(service:Service,category?:number|null){
 const key=vehicleCategories.find(item=>item.id===category)?.key;
 return (key?service.categoryPrices?.[key]:undefined)??service.price;
}
export type Product={id:string;name:string;price:number;stock:number;minimumStock:number;averageCost:number;unit:string;isSupply:boolean;supplierId?:string};
export type FlexSettings={stageNames:string[];packages:{name:string;serviceIds:string[]}[]};
export const defaultFlexSettings:FlexSettings={stageNames:['Aguardando','Lavando','Acabamento','Pronto'],packages:[]};
export type Branch={id:string;name:string};
export type Order={id:string;number:string;status:number;paymentStatus:number;total:number;vehicleId:string;employeeId?:string;branchId?:string|null;vehicleCategory?:number|null};
export class ApiError extends Error { constructor(public readonly status:number,message:string){super(message);this.name='ApiError'} }
export const currency=(n:number)=>new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(n);
// Set VITE_API_BASE_URL to the HTTPS API origin for a packaged Android build.
const configuredApiBase = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') || '';
export const API_BASE = configuredApiBase + '/api';
export async function api<T>(path:string,token:string,method='GET',body?:unknown):Promise<T>{
 const response=await fetch(API_BASE+path,{method,headers:{...(token?{Authorization:'Bearer '+token}:{}),'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
 if(!response.ok){
  const message=(await response.text()).replace(/^"|"$/g,'');
  if(response.status===401&&token)window.dispatchEvent(new CustomEvent('atwash:session-expired',{detail:{token}}));
  throw new ApiError(response.status,message||'Falha HTTP '+response.status);
 }
 if(response.status===204)return undefined as T;
 return response.json() as Promise<T>;
}
