# FC 27 DOM fixtures

Public FUTBIN DOM fragments captured on 2026-10-29 through a regular browser:

- `catalog-filtered.html`: https://www.futbin.com/27/players?eUnt=1&player_rating=80-99&page=1 — 30 rows. Unrelated stat/tooltips columns were omitted; rating, name link, version, position, price cells, selected filters and pagination were retained.
- `catalog-olise-unfiltered.html`: https://www.futbin.com/27/players?search=olise — includes Normal 62, TOTW 22947, HoloTOTW 22948 and SBC 23101. The test deliberately adds a filter confirmation to verify the secondary SBC check independently.
- `market-mbappe.html`: https://www.futbin.com/27/player/8/kylian-mbappe/market — canonical, both platform price boxes and auction tables, captured around 10:50 UTC. Tests use 11:00 UTC as the observation time. Relative quote ages are fixture inputs, not claims about current prices.

Scripts, inline handlers, images URLs and unrelated HTML attributes were removed. No authentication state or cookies are stored. Tests also generate synthetic fixtures for same-rating cards, zero/missing prices, objectives, malformed responses and profitable/unprofitable cases. These synthetic fixtures are not market evidence.

## Raw HTTP fixtures (no browser execution)

`catalog-http.html` and `market-http.html` were extracted from HTTP 200 response bodies on 2026-10-29 around 14:09 UTC using `curl_cffi` 0.17.0, TLS profile `safari`, with no browser process or JavaScript execution. Sources were the same filtered catalog and Mbappe market URLs above. They preserve the catalog/filter, canonical, price boxes and auction tables. Scripts, images URLs, inline handlers and unrelated attributes were stripped. The market test uses 14:12 UTC as observation time. Dates such as `Oct 29, 2:03 PM` were already present in the response body.
