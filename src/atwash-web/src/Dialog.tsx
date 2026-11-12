import {createContext,useContext,useEffect,useRef,useState,type ReactNode} from 'react';
type Kind='error'|'success'|'info'|'confirm';
type DialogData={kind:Kind;title:string;message:string;resolve?:(value:boolean)=>void};
type DialogApi={show:(message:string,kind?:Exclude<Kind,'confirm'>)=>void;confirm:(message:string)=>Promise<boolean>};
const Context=createContext<DialogApi|null>(null);
export function useDialog():DialogApi{const value=useContext(Context);if(!value)throw new Error('DialogProvider não encontrado');return value}
export function DialogProvider({children}:{children:ReactNode}){
 const [dialog,setDialog]=useState<DialogData|null>(null);
 const current=useRef<DialogData|null>(null);
 const queue=useRef<DialogData[]>([]);
 function enqueue(data:DialogData){if(current.current){queue.current.push(data);return}current.current=data;setDialog(data)}
 function close(accepted=false){const active=current.current;if(!active)return;active.resolve?.(accepted);const next=queue.current.shift()??null;current.current=next;setDialog(next)}
 useEffect(()=>{const key=(e:KeyboardEvent)=>{if(e.key==='Escape'&&current.current){e.preventDefault();close(false)}};window.addEventListener('keydown',key);return()=>window.removeEventListener('keydown',key)},[]);
 const show=(message:string,kind:Exclude<Kind,'confirm'>='info')=>{if(!message.trim())return;enqueue({kind,title:kind==='error'?'Atenção':kind==='success'?'Operação realizada':'Informação',message:message.replace(/^Error:\s*/i,'')})};
 const confirm=(message:string)=>new Promise<boolean>(resolve=>enqueue({kind:'confirm',title:'Confirmar operação',message,resolve}));
 return <Context.Provider value={{show,confirm}}>{children}{dialog&&<div className="modal-backdrop" role="presentation" onMouseDown={e=>{if(e.target===e.currentTarget&&dialog.kind!=='confirm')close(false)}}>
 <section className="panel login-error-modal" role={dialog.kind==='error'?'alertdialog':'dialog'} aria-modal="true" aria-labelledby="app-dialog-title" aria-describedby="app-dialog-message">
 <h2 id="app-dialog-title">{dialog.title}</h2><p id="app-dialog-message">{dialog.message}</p>
 <div className="actions" style={{justifyContent:'center'}}>{dialog.kind==='confirm'&&<button className="secondary" onClick={()=>close(false)}>Cancelar</button>}
 <button autoFocus onClick={()=>close(true)}>{dialog.kind==='confirm'?'Confirmar':'Entendi'}</button></div>
 </section></div>}</Context.Provider>
}
