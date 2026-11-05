# Deploy test instance

This setup uses a new Neon PostgreSQL database with demo data. It does not copy the existing local SQL Server database. Do not use real guest or payment data; this is a shared QA environment.

## Neon

1. Create a PostgreSQL project in Neon.
2. Copy its pooled connection string. Keep it private.

## Render

1. Create a Blueprint from this GitHub repository and select `render.yaml`.
2. Keep the web service name `lotus-pms-api`; the Vercel rewrite uses `https://lotus-pms-api.onrender.com`.
3. Set `ConnectionStrings__PmsDb` to the Neon connection string in the Render environment. It must include SSL, for example `SSL Mode=Require`.
4. Render generates `Jwt__Secret`. Keep it in Render; do not commit it.
5. Deploy and wait for `/api/health` to report `ok`.

The API initializes an empty schema and seeds demo records because `Database__InitializeForDemo=true`. It runs in Production mode, so Swagger is not exposed. The Render free instance may sleep when idle and take time to wake.

## Vercel

1. Import the same GitHub repository.
2. Set the project root directory to `frontend` and framework preset to Angular.
3. Use `npm run build` and output directory `dist/hotel-pms/browser`.
4. Keep the Render service name above, then deploy. `frontend/vercel.json` proxies `/api/*` to Render and routes the Angular app to `index.html`.

## Demo login

- Admin: `admin@pms.local`
- Password: `Demo@12345`

All testers share the same demo database, so changes made by one tester are visible to others.
