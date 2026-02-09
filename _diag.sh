#!/bin/bash
TOKEN=$(curl -s -X POST "https://it-tudes.tech/realms/ninvoices/protocol/openid-connect/token" \
  -d "grant_type=password" -d "client_id=ninvoices-web" \
  -d "username=testuser" -d "password=Test123!" | python3 -c 'import sys,json; print(json.load(sys.stdin)["access_token"])')

echo "=== Invoices API ==="
curl -s --max-time 15 -w "\nHTTP: %{http_code}\n" -H "Authorization: Bearer $TOKEN" "https://it-tudes.tech/nInvoices/api/invoices"

echo "=== API Logs ==="
echo "C0ncat0set606!" | sudo -S docker logs ninvoices-api-prod --since 30s 2>&1 | grep -i -A5 "error\|exception\|fail\|500"
