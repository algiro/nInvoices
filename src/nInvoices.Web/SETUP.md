# nInvoices Web - Setup Guide

## Development Environment

### Prerequisites
- Node.js 18+ and npm
- .NET 10 SDK
- Modern web browser (Chrome, Firefox, Edge)

### Configuration

#### Environment Variables (.env)
```bash
VITE_API_BASE_URL=
# Leave empty to use Vite proxy in development
# Set to your API URL for production (e.g., https://api.yourdomain.com)
```

#### API Configuration
The backend API runs on `http://localhost:5297` and includes CORS support for:
- `http://localhost:3000` (default Vite port)
- `http://localhost:3001` (fallback Vite port)
- `http://localhost:5173` (alternative Vite port)
- `http://localhost:5174` (alternative Vite port)

#### Vite Proxy
The Vite dev server proxies `/api/*` requests to the backend API:
```typescript
proxy: {
  '/api': {
    target: 'http://localhost:5297',
    changeOrigin: true
  }
}
```

### Starting the Application

1. **Start the API backend:**
   ```bash
   cd src/nInvoices.Api
   dotnet run
   ```
   API will be available at: http://localhost:5297

2. **Start the Vite dev server:**
   ```bash
   cd src/nInvoices.Web
   npm install  # First time only
   npm run dev
   ```
   Web app will be available at: http://localhost:3001 (or 3000)

### Browser-Specific Notes

#### Firefox
Firefox handles CORS and mixed content (HTTP/HTTPS) more strictly than Chrome. Ensure:
- The frontend uses the Vite proxy (relative URLs starting with `/api/`)
- Both frontend and backend are on the same protocol (HTTP in development)
- Clear browser cache if you see CORS errors after configuration changes

#### All Browsers
If you encounter errors:
1. Hard refresh (Ctrl+F5 or Cmd+Shift+R)
2. Clear browser cache and local storage
3. Check browser console for "[API Client] Base URL" log to verify configuration

### Common Issues

#### CORS Errors
**Symptom:** "Blocked by CORS policy" errors in browser console

**Solution:**
1. Verify API is running on port 5297
2. Verify `.env` file has empty `VITE_API_BASE_URL`
3. Check that requests go to `/api/*` (not absolute URLs)
4. Clear browser cache

#### Unicode/Encoding Errors
**Symptom:** "String contains an invalid character" DOMException

**Solution:**
- This was fixed in TemplateForm.vue by using HTML entities instead of raw Unicode characters
- If you see this error again, check for corrupted Unicode in Vue files

#### Port Already in Use
**Symptom:** Vite or API won't start

**Solution:**
```bash
# Windows: Find and kill process using port
Get-Process | Where-Object { $_.ProcessName -like "nInvoices.Api" } | Stop-Process -Force

# Or change port in launchSettings.json (API) or vite.config.ts (frontend)
```

### API Client Debug Logging
The API client logs its base URL on initialization. Check browser console:
```
[API Client] Base URL: (empty - using Vite proxy)
```

If you see a full URL here instead of empty, check your `.env` file.

### Production Build

1. Build the frontend:
   ```bash
   npm run build
   ```

2. Set `VITE_API_BASE_URL` in `.env.production` to your production API URL

3. Deploy:
   - Frontend: Deploy `dist/` folder to static hosting (Netlify, Vercel, etc.)
   - Backend: Deploy as ASP.NET Core application

### Testing
The application includes automated tests using Playwright. To run tests:

```bash
# Install Playwright (first time only)
npm install playwright
npx playwright install chromium

# Run tests
node test-templates.mjs
```
