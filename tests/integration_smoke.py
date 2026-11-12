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

def platform(method, path, data=None, expected=(200,), key="ci-platform-key-only-please-never-use-in-production-2026"):
    headers={"Content-Type":"application/json","X-ATWash-Platform-Key":key}
    body=None if data is None else json.dumps(data).encode()
    req=urllib.request.Request(BASE+"/platform"+path,data=body,headers=headers,method=method)
    try:
        with urllib.request.urlopen(req,timeout=15) as resp:
            status,content=resp.status,resp.read().decode()
    except urllib.error.HTTPError as err:
        status,content=err.code,err.read().decode()
    assert status in expected,f"Platform {method} {path}: expected {expected}, got {status}: {content[:500]}"
    return json.loads(content) if content and content[0] in '[{' else content

def main():
    global TOKEN
    login = request("POST", "/auth/login", {"email":"integration@atwash.test","password":"IntegrationOnlyPassword2026!"})
    TOKEN = login["accessToken"]
    assert login["deviceId"]
    assert any(d["id"]==login["deviceId"] for d in request("GET","/devices"))
    assert request("GET","/dashboard")["orders"] >= 0
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
    employee = request("POST", "/employees", {"name":"Lavador integração","role":"Lavador","phone":"11911111111","commissionPercent":10}, (201,))
    assigned = request("PUT", f"/orders/{oid}/employee", {"employeeId":employee["id"]})
    assert assigned["employeeId"] == employee["id"]
    assert any(e["id"] == employee["id"] for e in request("GET", "/employees"))
    access = request("POST", f"/employees/{employee['id']}/access",
        {"email":"washer-integration@atwash.test","password":"WasherIntegration2026!","role":3})
    assert access["email"] == "washer-integration@atwash.test"
    supplier = request("POST", "/suppliers", {"name":"Fornecedor integração","phone":"11999990000","taxId":"123"}, (201,))
    assert any(s["id"] == supplier["id"] for s in request("GET", "/suppliers"))
    supply = request("POST", "/products", {"name":"Shampoo insumo integração","price":0,"stock":10}, (201,))
    request("PUT", f"/products/{supply['id']}/details",
        {"name":"Shampoo insumo integração","price":0,"minimumStock":2,"averageCost":3.5,"unit":"ml","isSupply":True,"supplierId":supplier["id"]})
    request("PUT", f"/services/{services[0]['id']}/recipe", [{"productId":supply["id"],"quantity":2}])
    assert len(request("GET", f"/services/{services[0]['id']}/recipe")) == 1
    product = request("POST", "/products", {"name":"Shampoo teste integração","price":12.5,"stock":0}, (201,))
    initial_stock = product["stock"]
    request("POST", f"/products/{product['id']}/stock", {"quantity":3,"reason":"Reposição teste"}, (201,))
    sold = request("POST", f"/orders/{oid}/products", {"productId":product["id"],"quantity":2,"isConsumption":False}, (201,))
    assert sold["stock"] == initial_stock + 1
    consumed = request("POST", f"/orders/{oid}/products", {"productId":product["id"],"quantity":1,"isConsumption":True}, (201,))
    assert consumed["stock"] == initial_stock
    assert request("POST", f"/orders/{oid}/products",
        {"productId":product["id"],"quantity":100000,"isConsumption":False}, (409,)) is not None
    assert len(request("GET", f"/orders/{oid}/products")) == 2
    history = request("GET", f"/inventory/movements?productId={product['id']}")
    assert any(x["kind"] in ("Sale", 2) and x["quantity"] == -2 for x in history)
    assert any(x["kind"] in ("Consumption", 3) and x["quantity"] == -1 for x in history)
    order["total"] = sold["orderTotal"]  # updated order total from sale response
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
    assert next(p for p in request("GET", "/products") if p["id"] == supply["id"])["stock"] == 8
    assert any(m["kind"] in ("Consumption",3) and m["quantity"] == -2
               for m in request("GET", f"/inventory/movements?productId={supply['id']}"))
    assert request("GET", "/employees/performance") is not None
    request("PATCH", f"/orders/{oid}/status", {"status":3}, (409,))
    assert next(p for p in request("GET", "/products") if p["id"] == supply["id"])["stock"] == 8
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
    platform("GET","/tenants",expected=(401,),key="wrong")
    plan=platform("POST","/plans",{"name":"Teste","monthlyPrice":99.9,"maxDevices":2},(201,))
    tenant=platform("POST","/tenants",{"name":"Lava rápido integração",
        "ownerEmail":"new-tenant@atwash.test","ownerPassword":"IntegrationTenantPassword2026!",
        "planId":plan["id"],"expiresAt":None},(201,))
    assert tenant["name"]=="Lava rápido integração"
    assert any(t["id"]==tenant["id"] for t in platform("GET","/tenants"))
    platform("PUT",f"/tenants/{tenant['id']}/subscription",
        {"planId":plan["id"],"status":"suspended","expiresAt":None})
    request("POST","/auth/login",{"email":"new-tenant@atwash.test",
        "password":"IntegrationTenantPassword2026!"},(401,))
    platform("PUT",f"/tenants/{tenant['id']}/subscription",
        {"planId":plan["id"],"status":"active","expiresAt":None})
    assert request("POST","/auth/login",{"email":"new-tenant@atwash.test",
        "password":"IntegrationTenantPassword2026!"})["accessToken"]
    platform("PUT",f"/tenants/{tenant['id']}/status?active=false",expected=(200,))
    request("POST","/auth/login",{"email":"new-tenant@atwash.test",
        "password":"IntegrationTenantPassword2026!"},(401,))
    devices=request("GET","/devices")
    current=next(d for d in devices if d["id"]==login["deviceId"])
    assert current["revokedAt"] is None
    request("POST",f"/devices/{login['deviceId']}/revoke",{})
    request("GET","/auth/me",expected=(401,))
    print("PASS: migrations, auth, orders, mixed payments, idempotency, delivery, cash, Pix, reconciliation and refunds")

if __name__ == "__main__":
    main()
