#!/usr/bin/env bash
# Sets up "Sign in with Google" on a running nInvoices Keycloak, with approval:
# anyone can sign in with Google (or register), but nobody can use the app until an
# administrator grants them the "user" realm role.
#
# Safe to run more than once. Run it on the Docker host, next to the Keycloak container:
#
#   GOOGLE_CLIENT_ID=... GOOGLE_CLIENT_SECRET=... ./configure-google-login.sh
#
# Environment:
#   GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET  OAuth client from Google Cloud. Leave both empty to only
#                                           apply the approval rules (steps 1-2) without Google.
#   KC_CONTAINER   Keycloak container name               (default ninvoices-keycloak-prod)
#   KC_REALM       realm                                 (default ninvoices)
#   KC_ADMIN_USER, KC_ADMIN_PASSWORD
#                  master-realm admin. Default: the bootstrap admin from the container's own
#                  environment (KC_BOOTSTRAP_ADMIN_* or KEYCLOAK_ADMIN*).
#
# What it does:
#   1. Users who can use the app today only because "user" is a default role get it assigned
#      directly, so nobody loses access.
#   2. "user" is removed from the realm's default roles: new accounts start without access.
#   3. Adds (or updates) the Google identity provider.
#   4. Lists the accounts waiting for approval.
set -euo pipefail

CONTAINER="${KC_CONTAINER:-ninvoices-keycloak-prod}"
REALM="${KC_REALM:-ninvoices}"
ROLE="user"
DEFAULT_ROLES="default-roles-${REALM,,}"
KCADM=/opt/keycloak/bin/kcadm.sh
KCCONFIG=/tmp/kcadm-ninvoices.config
IDP_FILE=/tmp/ninvoices-google-idp.json
MAX_USERS=100000

# No -i: kc runs inside "while read" loops and must not swallow their input
kc() {
    docker exec "$CONTAINER" "$KCADM" "$@" --config "$KCCONFIG"
}

cleanup() {
    docker exec "$CONTAINER" rm -f "$KCCONFIG" "$IDP_FILE" >/dev/null 2>&1 || true
}
trap cleanup EXIT

if [[ -n "${GOOGLE_CLIENT_ID:-}" || -n "${GOOGLE_CLIENT_SECRET:-}" ]]; then
    # Google's values only use these characters; checking them keeps the JSON below safe to build by hand
    for value in "${GOOGLE_CLIENT_ID:-}" "${GOOGLE_CLIENT_SECRET:-}"; do
        if [[ ! "$value" =~ ^[A-Za-z0-9._-]+$ ]]; then
            echo "GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET must both be set (letters, digits, . _ -)." >&2
            exit 1
        fi
    done
fi

echo "== Signing in to Keycloak in container $CONTAINER"
docker exec -i \
    -e KC_ADMIN_USER="${KC_ADMIN_USER:-}" -e KC_ADMIN_PASSWORD="${KC_ADMIN_PASSWORD:-}" \
    "$CONTAINER" sh -c '
        user="${KC_ADMIN_USER:-${KC_BOOTSTRAP_ADMIN_USERNAME:-$KEYCLOAK_ADMIN}}"
        pass="${KC_ADMIN_PASSWORD:-${KC_BOOTSTRAP_ADMIN_PASSWORD:-$KEYCLOAK_ADMIN_PASSWORD}}"
        '"$KCADM"' config credentials --server http://localhost:8080 --realm master \
            --user "$user" --password "$pass" --config '"$KCCONFIG"' >/dev/null' || {
    echo "Could not sign in to Keycloak's master realm. The admin in the container's environment only" >&2
    echo "applies to the first start; if its password was changed since, pass the current one with" >&2
    echo "KC_ADMIN_USER and KC_ADMIN_PASSWORD (see the header of this script)." >&2
    exit 1
}

if ! kc get "roles/$ROLE" -r "$REALM" >/dev/null 2>&1; then
    echo "== Creating realm role \"$ROLE\""
    kc create roles -r "$REALM" -s name="$ROLE" -s 'description=Approved nInvoices user' >/dev/null
fi

default_roles=$(kc get "roles/$DEFAULT_ROLES/composites/realm" -r "$REALM" --fields name --format csv --noquotes | tr -d '\r')
if grep -qx "$ROLE" <<<"$default_roles"; then
    echo "== Step 1: giving \"$ROLE\" directly to everyone who has it through the default roles"
    granted=0
    while IFS= read -r user_id; do
        [[ -z "$user_id" ]] && continue
        kc add-roles -r "$REALM" --uid "$user_id" --rolename "$ROLE"
        granted=$((granted + 1))
    done < <(kc get "roles/$DEFAULT_ROLES/users" -r "$REALM" -q first=0 -q max="$MAX_USERS" \
        --fields id --format csv --noquotes | tr -d '\r')
    echo "   $granted user(s) keep their access"

    echo "== Step 2: removing \"$ROLE\" from the default roles (new accounts need approval)"
    kc remove-roles -r "$REALM" --rname "$DEFAULT_ROLES" --rolename "$ROLE"
else
    echo "== Steps 1-2: \"$ROLE\" is not a default role, nothing to change"
fi

if [[ -n "${GOOGLE_CLIENT_ID:-}" ]]; then
    echo "== Step 3: Google identity provider"
    printf '%s' "{
  \"alias\": \"google\",
  \"providerId\": \"google\",
  \"displayName\": \"Google\",
  \"enabled\": true,
  \"trustEmail\": true,
  \"storeToken\": false,
  \"firstBrokerLoginFlowAlias\": \"first broker login\",
  \"config\": {
    \"clientId\": \"$GOOGLE_CLIENT_ID\",
    \"clientSecret\": \"$GOOGLE_CLIENT_SECRET\",
    \"defaultScope\": \"openid email profile\",
    \"syncMode\": \"IMPORT\",
    \"hideOnLoginPage\": \"false\"
  }
}" | docker exec -i "$CONTAINER" sh -c "umask 077 && cat > $IDP_FILE"

    if kc get identity-provider/instances/google -r "$REALM" >/dev/null 2>&1; then
        kc update identity-provider/instances/google -r "$REALM" -f "$IDP_FILE"
        echo "   updated"
    else
        kc create identity-provider/instances -r "$REALM" -f "$IDP_FILE"
        echo "   created"
    fi
    echo "   Google redirect URI to allow: <public Keycloak URL>/realms/$REALM/broker/google/endpoint"
    echo "   (Keycloak admin console > $REALM > Identity providers > google shows the exact one)"
else
    echo "== Step 3: GOOGLE_CLIENT_ID not set, Google sign-in left as it is"
fi

echo "== Step 4: accounts waiting for approval (no \"$ROLE\" role assigned directly)"
approved=$(kc get "roles/$ROLE/users" -r "$REALM" -q first=0 -q max="$MAX_USERS" \
    --fields id --format csv --noquotes | tr -d '\r')
pending=0
while IFS=, read -r user_id username email; do
    [[ -z "$user_id" ]] && continue
    if ! grep -qx "$user_id" <<<"$approved"; then
        echo "   $username ${email:+<$email>}"
        pending=$((pending + 1))
    fi
done < <(kc get users -r "$REALM" -q first=0 -q max="$MAX_USERS" \
    --fields id,username,email --format csv --noquotes | tr -d '\r')
[[ $pending -eq 0 ]] && echo "   none"
echo "   Approve one: admin console > $REALM > Users > (user) > Role mapping > Assign role > $ROLE"

echo "== Done"
