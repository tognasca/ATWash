# Authentication foundation

- POST `/api/auth/login`: tenant ID, normalized email, password. Returns a 20-minute signed JWT.
- GET `/api/auth/me`: requires a valid bearer token.
- No anonymous account registration or default passwords are enabled.
- User accounts must be provisioned by an authorized administrator (provisioning workflow pending).
- Passwords must be hashed with ASP.NET Core `PasswordHasher<AppUser>`.
- Set a random `JWT_SECRET_KEY` of at least 32 UTF-8 bytes, outside source control.
- Refresh-token rotation, role policies, audit logs, tenant-scoped CRUD, and login lockout are **not yet implemented**.
- Never trust `tenant_id` submitted by a client for data access; obtain it from the validated JWT and check it in every query.
- The initial database migration was authored manually; validate the schema against EF Core's model and generate a model snapshot before production.
