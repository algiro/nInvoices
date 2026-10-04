# Sign in with Google, with approval

nInvoices can let people sign in with their Google account. Keycloak handles it ("identity
brokering"): the app still only talks to Keycloak, and every user keeps one stable Keycloak id,
whichever way they sign in.

Signing in is **not** the same as getting in. A new account, made with Google or with the
Keycloak registration form, starts **without access**. The person sees a page saying their account
is waiting for approval. Nothing is created for them until an administrator approves them.

## How access works

- The API requires the Keycloak realm role **`user`** on every endpoint. A signed-in account
  without it gets `403 Forbidden`.
- New accounts don't get `user` automatically: it is not one of the realm's default roles.
- **Approving someone means granting them `user`.** To revoke access, remove the role (or
  disable the user). Access tokens last 5 minutes, so the change applies within that time.
- `admin` is a separate role. It doesn't give access to the app on its own.

## 1. Create the Google OAuth client

In [Google Cloud Console](https://console.cloud.google.com/), in the project you use for nInvoices.
You can reuse the Gmail drafts project, but make a **separate** OAuth client for sign-in.

1. *APIs & Services > OAuth consent screen*: set the app name and support email. Sign-in only
   uses the `openid`, `email` and `profile` scopes, which don't need Google's verification. Set
   the publishing status to **In production**. In *Testing*, only the test users you list can
   sign in.
2. *APIs & Services > Credentials > Create credentials > OAuth client ID*, type **Web application**.
3. **Authorized redirect URI**: `<public Keycloak URL>/realms/ninvoices/broker/google/endpoint`.
   Use the same Keycloak URL the browser uses for sign-in (`VITE_KEYCLOAK_URL`). After step 2,
   the Keycloak admin console shows the exact value under *Identity providers > google >
   Redirect URI*.
4. Copy the **Client ID** and **Client secret**. Never commit them: `client_secret_*.json`
   is git-ignored.

## 2. Configure Keycloak

On the Docker host, with the Keycloak container running:

```bash
GOOGLE_CLIENT_ID=... GOOGLE_CLIENT_SECRET=... ./docker/keycloak/configure-google-login.sh
```

The script ([`docker/keycloak/configure-google-login.sh`](../docker/keycloak/configure-google-login.sh))
is safe to run again, for example to rotate the secret. It does the following:

1. Anyone who has access today only because `user` was a default role gets `user` assigned
   directly. **Existing users keep their access.**
2. It removes `user` from the realm's default roles, so new accounts need approval.
3. It creates or updates the `google` identity provider: trusted email, `openid email profile`.
4. It lists the accounts that are waiting for approval.

Options (environment variables): `KC_CONTAINER` (default `ninvoices-keycloak-prod`), `KC_REALM`
(default `ninvoices`), and `KC_ADMIN_USER` / `KC_ADMIN_PASSWORD` if the bootstrap admin from the
container's environment no longer exists. Without `GOOGLE_CLIENT_ID`, it applies only steps 1, 2
and 4.

Fresh installs that import [`docker/keycloak/realm-export.json`](../docker/keycloak/realm-export.json)
already start without `user` as a default role. Only step 3 is needed there.

## 3. Approve people

1. They sign in with Google and land on *"Your account is waiting for approval"*.
2. In the Keycloak admin console, go to realm **ninvoices > Users** and open the user.
3. Go to *Role mapping > Assign role*, filter by realm roles, pick **`user`** and click *Assign*.
4. They click *Check again* (or reload) and they're in, with their own empty workspace.

Running the script again (step 4 of its output) lists who is still waiting.

## Accounts that already exist

If someone already has a password account and then uses *Sign in with Google* with the same
email, Keycloak's standard *first broker login* flow asks them to confirm by signing in with
their password once. The two are then linked to one user. Same id, same data, same approval.

## Turning off the registration form

Approval also applies to people who register with a password. If you only want Google
sign-in, turn off *Realm settings > Login > User registration*. Google sign-in still works,
and accounts still need approval.
