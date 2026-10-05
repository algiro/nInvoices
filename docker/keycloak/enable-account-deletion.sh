#!/usr/bin/env bash
# Lets nInvoices users delete their own sign-in account. After nInvoices has deleted their data
# (Settings > Delete account), the web app sends them to Keycloak's "Delete account" page, which
# needs two things this script sets up:
#   1. the "Delete Account" required action, enabled in the realm;
#   2. the "delete-account" role of the "account" client, given to everyone through the realm's
#      default roles (existing users get it too, since they all have the default roles).
#
# Safe to run more than once. Run it on the Docker host, next to the Keycloak container:
#
#   ./enable-account-deletion.sh
#
# Environment: KC_CONTAINER (default ninvoices-keycloak-prod), KC_REALM (default ninvoices), and
# KC_ADMIN_USER / KC_ADMIN_PASSWORD when the bootstrap admin from the container's environment no
# longer works (see configure-google-login.sh).
set -euo pipefail

CONTAINER="${KC_CONTAINER:-ninvoices-keycloak-prod}"
REALM="${KC_REALM:-ninvoices}"
DEFAULT_ROLES="default-roles-${REALM,,}"
KCADM=/opt/keycloak/bin/kcadm.sh
KCCONFIG=/tmp/kcadm-ninvoices.config

kc() {
    docker exec "$CONTAINER" "$KCADM" "$@" --config "$KCCONFIG"
}

cleanup() {
    docker exec "$CONTAINER" rm -f "$KCCONFIG" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "== Signing in to Keycloak in container $CONTAINER"
docker exec -i \
    -e KC_ADMIN_USER="${KC_ADMIN_USER:-}" -e KC_ADMIN_PASSWORD="${KC_ADMIN_PASSWORD:-}" \
    "$CONTAINER" sh -c '
        user="${KC_ADMIN_USER:-${KC_BOOTSTRAP_ADMIN_USERNAME:-$KEYCLOAK_ADMIN}}"
        pass="${KC_ADMIN_PASSWORD:-${KC_BOOTSTRAP_ADMIN_PASSWORD:-$KEYCLOAK_ADMIN_PASSWORD}}"
        '"$KCADM"' config credentials --server http://localhost:8080 --realm master \
            --user "$user" --password "$pass" --config '"$KCCONFIG"' >/dev/null' || {
    echo "Could not sign in to Keycloak's master realm: pass the current admin with KC_ADMIN_USER and KC_ADMIN_PASSWORD." >&2
    exit 1
}

echo "== Step 1: the \"Delete Account\" required action"
if kc get authentication/required-actions/delete_account -r "$REALM" >/dev/null 2>&1; then
    kc update authentication/required-actions/delete_account -r "$REALM" -s enabled=true
else
    # Not registered in this realm yet (realms imported from a file list only some actions)
    kc create authentication/register-required-action -r "$REALM" -s providerId=delete_account -s name="Delete Account"
    kc update authentication/required-actions/delete_account -r "$REALM" -s enabled=true
fi
echo "   enabled"

echo "== Step 2: everyone may delete their own account"
if kc get "roles/$DEFAULT_ROLES/composites/clients/$(kc get clients -r "$REALM" -q clientId=account --fields id --format csv --noquotes | tr -d '\r')" \
        -r "$REALM" --fields name --format csv --noquotes | tr -d '\r' | grep -qx delete-account; then
    echo "   already part of $DEFAULT_ROLES"
else
    kc add-roles -r "$REALM" --rname "$DEFAULT_ROLES" --cclientid account --rolename delete-account
    echo "   added account/delete-account to $DEFAULT_ROLES"
fi

echo "== Done"
