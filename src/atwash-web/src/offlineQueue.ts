/**
 * Durable offline operation queue for non-financial commands.
 * A server endpoint must implement OperationId idempotency before replay is enabled.
 * Never enqueue Pix confirmations, refunds, card authorizations or cash movements.
 */
export type OfflineOperation = {
  operationId: string;
  tenantId: string;
  deviceId: string;
  path: string;
  method: "POST" | "PUT" | "PATCH";
  body: unknown;
  createdAt: string;
  attempts: number;
};
const DB_NAME = "atwash-offline-v1";
const STORE = "operations";
function database(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE, { keyPath: "operationId" });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}
async function transaction<T>(mode: IDBTransactionMode, operation: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await database();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, mode);
    const request = operation(tx.objectStore(STORE));
    tx.oncomplete = () => { db.close(); resolve(request.result); };
    tx.onerror = () => { db.close(); reject(tx.error); };
    tx.onabort = () => { db.close(); reject(tx.error); };
  });
}
const SAFE_PATH = /^\/api\/(customers|vehicles|orders)(\/[^?#]*)?$/;
export async function enqueueOfflineOperation(
  input: Omit<OfflineOperation, "operationId" | "createdAt" | "attempts">
): Promise<OfflineOperation> {
  if (!SAFE_PATH.test(input.path) || !["POST", "PUT", "PATCH"].includes(input.method))
    throw new Error("Esta operação exige conexão com o servidor.");
  if (!input.tenantId || !input.deviceId) throw new Error("Empresa ou dispositivo não identificado.");
  const entry: OfflineOperation = {
    ...input, operationId: crypto.randomUUID(), createdAt: new Date().toISOString(), attempts: 0
  };
  await transaction("readwrite", store => store.add(entry));
  return entry;
}
export async function listOfflineOperations(): Promise<OfflineOperation[]> {
  const entries = await transaction("readonly", store => store.getAll() as IDBRequest<OfflineOperation[]>);
  return entries.sort((a, b) => a.createdAt.localeCompare(b.createdAt));
}
export async function removeOfflineOperation(operationId: string): Promise<void> {
  await transaction("readwrite", store => store.delete(operationId));
}
export async function clearOfflineOperations(): Promise<void> {
  await transaction("readwrite", store => store.clear());
}
/**
 * Explicitly no automatic replay here: current API endpoints must first support
 * durable idempotency and conflict responses. Replay without that guarantee
 * could duplicate orders and inventory changes.
 */
