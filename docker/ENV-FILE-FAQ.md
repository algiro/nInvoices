# Understanding the .env File - FAQ

## ❓ "How can I set passwords before PostgreSQL is configured?"

**Short Answer:** You're not setting passwords for an existing database - you're **choosing** the passwords that will be automatically set when the services start for the first time.

## 🔄 The Setup Flow

### Step 1: You Choose Passwords (in .env)
```env
# You decide what passwords to use:
POSTGRES_PASSWORD=MySecurePassword123!
KEYCLOAK_ADMIN_PASSWORD=AdminPassword456!
```

### Step 2: Docker Compose Starts Services
```bash
docker-compose -f docker-compose.dev.yml up -d
```

### Step 3: Containers Read .env and Configure Themselves

**PostgreSQL Container:**
1. Sees `POSTGRES_USER=ninvoices_user`
2. Sees `POSTGRES_PASSWORD=MySecurePassword123!`
3. Creates a new database user "ninvoices_user" with that password
4. Creates the "ninvoices_db" database
5. Now the password IS `MySecurePassword123!`

**Keycloak Container:**
1. Sees `KEYCLOAK_ADMIN=admin`
2. Sees `KEYCLOAK_ADMIN_PASSWORD=AdminPassword456!`
3. Creates admin account with that password
4. Now you can login with admin / AdminPassword456!

## 💡 Think of it Like This

The `.env` file is like a **configuration template** or **installation settings**:

```
┌─────────────────────────────────────────────────────────┐
│ .env File (Your Choices)                                │
├─────────────────────────────────────────────────────────┤
│ POSTGRES_PASSWORD=MyPassword123                         │
│                                                          │
│ This means:                                             │
│ "When you create PostgreSQL, set the password to this" │
└─────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│ Docker Compose reads .env                               │
│ Passes values to containers as environment variables    │
└─────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│ PostgreSQL Container Starts                             │
│ - Checks if database exists: NO                         │
│ - Creates new database user with $POSTGRES_PASSWORD     │
│ - Password is now: MyPassword123                        │
└─────────────────────────────────────────────────────────┘
```

## 🔐 Analogy

It's like installing software on your computer:

**During Installation:**
- Installer asks: "Choose your password"
- You type: "SecurePassword123"
- Installer creates account with that password
- From now on, you login with "SecurePassword123"

**With Docker:**
- .env asks: "Choose your password"
- You write: `POSTGRES_PASSWORD=SecurePassword123`
- Docker creates database with that password
- From now on, the password IS "SecurePassword123"

## 📝 Complete Example

### 1. Before Starting (Services Don't Exist Yet)

```env
# .env file - You're choosing what the passwords WILL BE
POSTGRES_USER=ninvoices_user
POSTGRES_PASSWORD=IChooseThisPassword123!
POSTGRES_DB=ninvoices_db
```

### 2. Start Docker Compose

```bash
docker-compose up -d
```

### 3. Docker Creates Everything

PostgreSQL starts and runs (internally):
```sql
-- PostgreSQL automatically does this:
CREATE USER ninvoices_user WITH PASSWORD 'IChooseThisPassword123!';
CREATE DATABASE ninvoices_db OWNER ninvoices_user;
```

### 4. Now You Can Connect

```bash
# This works because the password WAS SET to what you chose:
psql -U ninvoices_user -d ninvoices_db
Password: IChooseThisPassword123!
```

## ⚠️ Important Notes

### First Time Startup
- Passwords are set from .env on **first startup only**
- Data is persisted in `docker/volumes/postgres/`
- Changing .env later won't change existing passwords

### If You Change .env After First Run

```bash
# Wrong - won't work!
# 1. Edit .env with new password
# 2. Restart containers
# Result: Old password still active (data persists)

# Right - to change password after first run:
# Option A: Delete volumes and start fresh
docker-compose down -v  # ⚠️ DELETES ALL DATA!
# Then edit .env and start again

# Option B: Change password manually in running container
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user
ALTER USER ninvoices_user WITH PASSWORD 'NewPassword';
# Then update .env to match
```

### Checking Current Password

You can't "see" the current password, but you can test it:

```bash
# Try connecting - if it works, password is correct:
docker exec -it ninvoices-postgres-dev psql -U ninvoices_user -d ninvoices_db

# Or from host machine:
PGPASSWORD=MySecurePassword123! psql -h localhost -U ninvoices_user -d ninvoices_db
```

## 🎯 Quick Reference

| When | What .env Does |
|------|----------------|
| **Before first startup** | You choose passwords that will be set |
| **First startup** | Docker creates users/databases with those passwords |
| **After startup** | .env is just documentation (data persists in volumes) |
| **Changed .env** | Won't affect existing services unless you delete volumes |

## 🔄 Common Workflows

### Starting Fresh Project (You Are Here)
```bash
# 1. Choose passwords in .env (services don't exist yet)
echo "POSTGRES_PASSWORD=MyNewPassword" > .env

# 2. Start services (passwords will be set)
docker-compose up -d

# 3. Services now use those passwords
```

### Existing Project (Passwords Already Set)
```bash
# 1. Get .env from team/documentation
#    (contains the passwords that were ALREADY set)

# 2. Start services (connects with existing passwords)
docker-compose up -d

# 3. Services use passwords that were set during initial setup
```

### Forgot Password?
```bash
# Only option: Delete data and start over
docker-compose down -v  # ⚠️ Deletes all data!
# Edit .env with new password
docker-compose up -d
# Fresh start with new password
```

## 💭 Summary

**The .env file is not configuring existing services - it's configuring NEW services that Docker will create.**

Think of it as:
- ❌ NOT: "Enter your PostgreSQL password to connect"
- ✅ YES: "Choose the password for the new PostgreSQL that will be created"

**You pick the passwords BEFORE starting Docker, and Docker uses those choices to set up the services.**
