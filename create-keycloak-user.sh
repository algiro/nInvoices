#!/bin/bash

# Get admin token
echo "Getting admin token..."
TOKEN=$(curl -s -X POST 'https://DOMAIN_PLACEHOLDER/realms/master/protocol/openid-connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'username=admin' \
  -d 'password=Albagnano2026Pass' \
  -d 'grant_type=password' \
  -d 'client_id=admin-cli' \
  | python3 -c 'import sys, json; data=json.load(sys.stdin); print(data.get("access_token", ""))' 2>/dev/null)

if [ -z "$TOKEN" ]; then
  echo "Failed to get admin token"
  exit 1
fi

echo "Token obtained successfully"

# Check if user exists
echo "Checking if testuser exists..."
USERS=$(curl -s -X GET 'https://DOMAIN_PLACEHOLDER/admin/realms/ninvoices/users?username=testuser' \
  -H "Authorization: Bearer $TOKEN")

USER_COUNT=$(echo "$USERS" | python3 -c 'import sys, json; print(len(json.load(sys.stdin)))' 2>/dev/null)

if [ "$USER_COUNT" = "0" ]; then
  echo "User does not exist. Creating testuser..."
  
  # Create user
  curl -s -X POST 'https://DOMAIN_PLACEHOLDER/admin/realms/ninvoices/users' \
    -H "Authorization: Bearer $TOKEN" \
    -H 'Content-Type: application/json' \
    -d '{
      "username": "testuser",
      "enabled": true,
      "emailVerified": true,
      "firstName": "Test",
      "lastName": "User",
      "email": "testuser@ninvoices.local",
      "credentials": [{
        "type": "password",
        "value": "Test123!",
        "temporary": false
      }]
    }'
  
  echo ""
  echo "User created successfully!"
else
  echo "User already exists. Getting user ID..."
  USER_ID=$(echo "$USERS" | python3 -c 'import sys, json; data=json.load(sys.stdin); print(data[0]["id"] if data else "")' 2>/dev/null)
  
  if [ -n "$USER_ID" ]; then
    echo "Resetting password for user ID: $USER_ID"
    
    # Reset password
    curl -s -X PUT "https://DOMAIN_PLACEHOLDER/admin/realms/ninvoices/users/$USER_ID/reset-password" \
      -H "Authorization: Bearer $TOKEN" \
      -H 'Content-Type: application/json' \
      -d '{
        "type": "password",
        "value": "Test123!",
        "temporary": false
      }'
    
    echo ""
    echo "Password reset successfully!"
  fi
fi

echo ""
echo "Done! You can now login with:"
echo "Username: testuser"
echo "Password: Test123!"
