# README hero GIF: script and capture list

Goal: in about 20 seconds, show **worked days → real invoice preview → PDF**, the thing no spreadsheet does.

## Setup (once)
1. Run a clean instance, so the recording has no real data: the fast loop (SQLite + DevAuth) with a new
   database file, e.g. `ConnectionStrings__Default="Data Source=demo.db"` and `dotnet ef database update --connection "Data Source=demo.db"`.
2. Settings › Data & account › **Load sample data**. The days are filled for the two months before today,
   so record in the same session, not from an old database.
3. Pick the theme you want the project to be known by (*Lagoon* matches the README's first screenshot) and
   set the browser window to **1280×720**, zoom 100%, no bookmarks bar, no other tabs.
4. Do one dry run first: the first PDF render downloads headless Chrome, which makes the preview slow once.

## Tool
ScreenToGif (Windows, free) or ShareX. Capture 15 fps, crop to the app window, then optimise: target
**under 5 MB**, 800–1000 px wide (GitHub shows it at about 880 px). Also export an MP4/WebM for the landing page.

## Shots (about 20 s)
| s | Do | Show |
|---|---|---|
| 0-2 | Click **New invoice** | The wizard, step 1 |
| 2-5 | Choose *Northwind Studio (sample)*, move the period back one month | Customer + period |
| 5-9 | **Next: Time** | The calendar already filled, the live subtotal (22 days, €10,560) in the footer |
| 9-11 | Click one day, press `H` (holiday) | The subtotal drops by a day: this is the "only touch the days that differ" idea |
| 11-13 | **Next: Expenses**, then **Next: Review** | Skip expenses quickly |
| 13-18 | Review step | The real invoice preview with the totals; switch to the **Timesheet** tab once |
| 18-20 | **Generate invoice** | The draft invoice opens. Stop. |

Keep the pointer slow and deliberate, no scrolling, no typing except the customer choice.

## Captions (burn in or add as text on the landing page)
1. "Mark the days you worked"
2. "See the real invoice before it exists"
3. "One click: invoice + timesheet PDF"

## Stills to capture alongside (PNG, same window size)
- Invoice preview, zoomed to the page (use the *100%* toggle): the polished layout.
- The Time step calendar with a split day open on the right.
- The dashboard after generating two invoices.
- Settings › Country & taxes with one country's rules on.

Save them in `Docs/screenshots/` with the theme in the name, as the existing ones do, and embed the GIF at the top of the README under the badges.

## Do not show
Real customer names, real amounts, the Keycloak sign-in page, or the sample-data note; all names in the
sample data are fictional, which the README's screenshot note already says.
