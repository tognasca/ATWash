/** UUID v4 for payment idempotency. Works on HTTP LAN origins where crypto.randomUUID is unavailable. */
export function createOperationId(): string {
  const bytes = new Uint8Array(16);
  if (typeof globalThis.crypto?.getRandomValues !== 'function') {
    throw new Error('Este navegador não oferece geração criptográfica de identificadores. Use HTTPS ou localhost para registrar pagamentos.');
  }
  globalThis.crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
  return [hex.slice(0,8), hex.slice(8,12), hex.slice(12,16), hex.slice(16,20), hex.slice(20)].join('-');
}
