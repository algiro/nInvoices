#!/bin/bash
TOKEN=$(curl -s -X POST "https://it-tudes.tech/realms/ninvoices/protocol/openid-connect/token" \
  -d "grant_type=password" -d "client_id=ninvoices-web" \
  -d "username=testuser" -d "password=Test123!" 2>/dev/null \
  | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d.get("access_token","NOTOKEN"))' 2>/dev/null)

echo "=== Export customers (which works) ==="
curl -s --max-time 15 -w "\nHTTP: %{http_code}" -H "Authorization: Bearer $TOKEN" "https://it-tudes.tech/nInvoices/api/importexport/customers" 2>/dev/null | python3 -c '
import sys, json
try:
    data = json.load(sys.stdin)
    print(json.dumps(data, indent=2)[:500])
except Exception as e:
    print(f"Parse error: {e}")
    print(sys.stdin.read()[:200])
' 2>/dev/null

echo ""
echo "=== Settings ==="
curl -s --max-time 15 -w "HTTP: %{http_code}" -H "Authorization: Bearer $TOKEN" "https://it-tudes.tech/nInvoices/api/settings/invoice" 2>/dev/null