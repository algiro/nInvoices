#!/bin/bash
# Check token audience

TOKEN=$(curl -s -X POST "https://DOMAIN_PLACEHOLDER/realms/ninvoices/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=ninvoices-web&username=testuser&password=Test123!&scope=openid" \
  | python3 -c "import sys,json;print(json.load(sys.stdin).get('access_token',''))")

# Decode JWT payload (base64 middle part)
echo "$TOKEN" | cut -d. -f2 | python3 -c "
import sys,base64,json
data = sys.stdin.read().strip()
# Add padding
data += '=' * (4 - len(data) % 4)
decoded = base64.urlsafe_b64decode(data)
payload = json.loads(decoded)
print('Issuer:', payload.get('iss'))
print('Audience:', payload.get('aud'))
print('Azp:', payload.get('azp'))
print('Scope:', payload.get('scope'))
print('Resource access:', json.dumps(payload.get('resource_access', {}), indent=2))
"
