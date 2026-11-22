import asyncio, pathlib, sys
from playwright.async_api import async_playwright

MOCKUP = sys.argv[1]
OUT = pathlib.Path(sys.argv[2])
SIZES = ["1600x900", "1280x800", "1128x752"]

async def main():
    OUT.mkdir(parents=True, exist_ok=True)
    async with async_playwright() as p:
        browser = await p.chromium.connect_over_cdp("http://localhost:29229")
        page = await browser.contexts[0].new_page()
        for size in SIZES:
            w, h = map(int, size.split("x"))
            await page.set_viewport_size({"width": w, "height": h})
            await page.goto(pathlib.Path(MOCKUP).resolve().as_uri() + "#shot")
            await page.evaluate("document.body.classList.add('reduced')")
            await page.evaluate(f"document.querySelector('[data-size=\"{size}\"]').click()")
            for theme in ["light", "dark"]:
                await page.evaluate(f"document.querySelector('[data-t=\"{theme}\"]').click()")
                for view in ["today", "board", "nomi"]:
                    await page.evaluate(f"document.querySelector('[data-v=\"{view}\"]').click()")
                    await page.wait_for_timeout(1200)
                    await page.locator(".win").screenshot(path=str(OUT / f"{size}-{theme}-{view}.png"))
        await page.close()

asyncio.run(main())
