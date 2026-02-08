#!/bin/bash
# Test API authentication flow

# Step 1: Get admin token
echo "Step 1: Getting Keycloak admin token..."
ADMIN_TOKEN=$(curl -s -X POST "https://DOMAIN_PLACEHOLDER/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=admin-cli&username=admin&password=Albagnano2026Pass" \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('access_token',''))")

if [ -z "$ADMIN_TOKEN" ]; then
  echo "ERROR: Failed to get admin token"
  exit 1
fi
echo "Admin token obtained (length: ${#ADMIN_TOKEN})"

# Step 2: Enable direct access grants on ninvoices-web client
echo ""
echo "Step 2: Enabling direct access grants on ninvoices-web client..."
CLIENT_ID_UUID=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
  "https://DOMAIN_PLACEHOLDER/admin/realms/ninvoices/clients?clientId=ninvoices-web" \
  | python3 -c "import sys,json;clients=json.load(sys.stdin);print(clients[0]['id'] if clients else '')")

if [ -z "$CLIENT_ID_UUID" ]; then
  echo "ERROR: Client ninvoices-web not found"
  exit 1
fi
echo "Client UUID: $CLIENT_ID_UUID"

curl -s -X PUT "https://DOMAIN_PLACEHOLDER/admin/realms/ninvoices/clients/$CLIENT_ID_UUID" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"clientId\":\"ninvoices-web\",\"directAccessGrantsEnabled\":true}"
echo "Direct access grants enabled"

# Step 3: Get user token
echo ""
echo "Step 3: Getting user token..."
TOKEN_RESPONSE=$(curl -s -X POST "https://DOMAIN_PLACEHOLDER/realms/ninvoices/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=ninvoices-web&username=testuser&password=Test123!&scope=openid")

USER_TOKEN=$(echo "$TOKEN_RESPONSE" | python3 -c "import sys,json;d=json.load(sys.stdin);print(d.get('access_token',''))")
ERROR=$(echo "$TOKEN_RESPONSE" | python3 -c "import sys,json;d=json.load(sys.stdin);print(d.get('error',''))")

if [ -z "$USER_TOKEN" ]; then
  echo "ERROR: Failed to get user token. Error: $ERROR"
  echo "Full response: $TOKEN_RESPONSE"
  exit 1
fi
echo "User token obtained (length: ${#USER_TOKEN})"

# Step 4: Test API with token
echo ""
echo "Step 4: Testing API endpoints with token..."

# Test health (should work without auth)
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://DOMAIN_PLACEHOLDER/nInvoices/api/health")
echo "Health endpoint (no auth): $HTTP_CODE"

# Test authenticated endpoint
HTTP_CODE=$(curl -s -o /tmp/api-resp.txt -w "%{http_code}" \
  -H "Authorization: Bearer $USER_TOKEN" \
  "https://DOMAIN_PLACEHOLDER/nInvoices/api/customers")
echo "Customers endpoint (with auth): $HTTP_CODE"
echo "Response: $(cat /tmp/api-resp.txt | head -c 200)"

echo ""
echo "Done!"
