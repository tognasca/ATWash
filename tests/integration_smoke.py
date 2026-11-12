"""Integration smoke tests against an isolated PostgreSQL database and running API."""
import json
import urllib.error
import urllib.request
import uuid

BASE = "http://127.0.0.1:5098/api"
TOKEN = ""

def request(method, path, data=None, expected=(200,)):
    headers = {"Content-Type": "application/json"}
    if TOKEN:
        headers["Authorization"] = "Bearer " + TOKEN
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(BASE + path, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            status, content = resp.status, resp.read().decode()
    except urllib.error.HTTPError as err:
        status, content = err.code, err.read().decode()
    assert status in expected, f"{method} {path}: expected {expected}, got {status}: {content[:1000]}"
    return json.loads(content) if content and content[0] in '[{' else content

def main():
    global TOKEN
    login = request("POST", "/auth/login", {"email":"integration@atwash.test","password":"IntegrationOnlyPassword2026!"})
    TOKEN = login["accessToken"]
    assert request("GET", "/auth/me")["role"] == "Owner"
    services = request("GET", "/services")
    assert services, "Default services must be seeded for new tenant"
    customer_input={"name":"Cliente Integração","phone":"11999999999","clientId":str(uuid.uuid4())}
    customer = request("POST", "/customers", customer_input, (201,))
    assert request("POST", "/customers", customer_input, (200,))["id"] == customer["id"]
    vehicle_input={"customerId":customer["id"],"plate":"ABC1D23","model":"Teste","clientId":str(uuid.uuid4())}
    vehicle = request("POST", "/vehicles", vehicle_input, (201,))
    assert request("POST", "/vehicles", vehicle_input, (200,))["id"] == vehicle["id"]
    order_input={"vehicleId":vehicle["id"],"serviceIds":[services[0]["id"]],"clientId":str(uuid.uuid4())}
    order = request("POST", "/orders", order_input, (201,))
    assert request("POST", "/orders", order_input, (200,))["id"] == order["id"]
    oid = order["id"]
    assert request("GET", f"/orders/{oid}/payments")["remaining"] == order["total"]
    request("POST", "/cash/open", {"openingFloat":100}, (201,))
    amount = round(order["total"] / 2, 2)
    rest = round(order["total"] - amount, 2)
    if amount == 0 or rest == 0:
        raise AssertionError("Default service must have positive price")
    op = str(uuid.uuid4())
    payment = {"operationId":op,"method":0,"amount":amount,"tendered":amount}
    request("POST", f"/orders/{oid}/payments", payment, (201,))
    request("POST", f"/orders/{oid}/payments", payment, (200,))
    assert request("GET", f"/orders/{oid}/payments")["remaining"] == rest
    request("POST", f"/orders/{oid}/payments", {"operationId":str(uuid.uuid4()),"method":1,"amount":rest,"tendered":rest,"reference":"CI-NSU-123"}, (201,))
    assert request("GET", f"/orders/{oid}/payments")["remaining"] == 0
    assert request("POST", f"/orders/{oid}/deliver", expected=(409,)), "must reject premature delivery"
    for status in (1,2,3):
        request("PATCH", f"/orders/{oid}/status", {"status":status})
    request("PATCH", f"/orders/{oid}/status", {"status":4}, (409,))
    request("POST", f"/orders/{oid}/deliver", expected=(200,))
    cash = request("GET", "/cash/current")
    assert cash["open"] and cash["cashSales"] == amount
    request("POST", "/cash/movements", {"kind":0,"amount":20,"reason":"Suprimento de teste"}, (201,))
    closed = request("POST", "/cash/close", {"countedCash":120+amount,"note":"Integração"})
    assert closed["difference"] == 0, closed
    assert request("GET", "/cash/current")["open"] is False
    # Refund and reconciliation cannot silently create paid/delivered orders.
    order2 = request("POST", "/orders", {"vehicleId":vehicle["id"],"serviceIds":[services[0]["id"]]}, (201,))
    oid2 = order2["id"]
    total = order2["total"]
    request("PUT", "/settings/pix", {"pixKey":"pix@atwash.test","merchantName":"ATWash Teste","merchantCity":"Sao Paulo"})
    pix = request("GET", f"/orders/{oid2}/pix")
    assert pix["payload"].startswith("000201") and len(pix["payload"]) > 60
    assert pix["automaticConfirmation"] is False
    pix_pay = request("POST", f"/orders/{oid2}/payments",
        {"operationId":str(uuid.uuid4()),"method":2,"amount":total,"tendered":total,"reference":"PIX-CI-123"}, (201,))
    payment_id = pix_pay["id"]
    request("POST", f"/payments/{payment_id}/reconcile", {"bankReference":"BANK-CI-123"})
    request("POST", f"/payments/{payment_id}/reconcile", {"bankReference":"BANK-CI-123"}, (409,))
    refund_amount = round(total / 2, 2)
    request("POST", f"/payments/{payment_id}/refund",
        {"amount":refund_amount,"reason":"Teste de devolução","externalReference":"REF-CI-123"})
    assert request("GET", f"/orders/{oid2}/payments")["remaining"] == refund_amount
    assert request("POST", f"/orders/{oid2}/deliver", expected=(409,)), "refunded order must not deliver"
    request("POST", f"/orders/{oid2}/payments",
        {"operationId":str(uuid.uuid4()),"method":1,"amount":refund_amount,"tendered":refund_amount,"reference":"CARD-CI-456"}, (201,))
    assert request("GET", f"/orders/{oid2}/payments")["remaining"] == 0
    request("POST", "/cash/open", {"openingFloat":0}, (201,))
    request("POST", f"/payments/{payment_id}/refund",
        {"amount":1,"reason":"Excesso","externalReference":"REF-CI-123"}, (409,))
    request("POST", "/cash/close", {"countedCash":0,"note":"Segundo fechamento"})
    print("PASS: migrations, auth, orders, mixed payments, idempotency, delivery, cash, Pix, reconciliation and refunds")

if __name__ == "__main__":
    main()
