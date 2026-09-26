import { chromium } from '@playwright/test';

async function testPdfRegeneration() {
    console.log('🧪 Testing PDF regeneration feature...');
    
    const browser = await chromium.launch({ headless: false, slowMo: 500 });
    const context = await browser.newContext();
    const page = await context.newPage();
    
    // Listen for console messages
    page.on('console', msg => {
        if (msg.type() === 'error') {
            console.error('❌ Browser console error:', msg.text());
        }
    });

    try {
        // Navigate to app
        await page.goto('http://localhost:3001');
        await page.waitForLoadState('networkidle');
        console.log('✓ Application loaded');

        // Go to Customers
        await page.click('a[href="/customers"]');
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(1000);
        console.log('✓ Navigated to customers page');

        // Select first customer
        const firstCustomer = await page.locator('.customer-row, tr').first();
        await firstCustomer.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(1000);
        console.log('✓ Selected customer');

        // Go to Invoices tab
        const invoicesTab = page.locator('button, a').filter({ hasText: /Invoices/i });
        await invoicesTab.click();
        await page.waitForTimeout(1000);
        console.log('✓ Navigated to invoices tab');

        // Click on first invoice
        const firstInvoice = await page.locator('.invoice-row, tr').first();
        await firstInvoice.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(1500);
        console.log('✓ Opened invoice details');

        // Look for regenerate invoice button
        const regenerateInvoiceBtn = page.locator('button').filter({ hasText: /Regenerate.*Invoice.*PDF/i });
        if (await regenerateInvoiceBtn.isVisible()) {
            console.log('✓ Found "Regenerate Invoice PDF" button');
            
            // Click the button
            await regenerateInvoiceBtn.click();
            await page.waitForTimeout(500);
            
            // Check if confirmation dialog appears
            const dialogText = await page.textContent('body');
            if (dialogText.includes('regenerate') || dialogText.includes('confirm')) {
                console.log('✓ Confirmation dialog appeared');
                
                // Confirm the action
                const confirmBtn = page.locator('button').filter({ hasText: /yes|confirm|ok/i }).first();
                if (await confirmBtn.isVisible()) {
                    await confirmBtn.click();
                    await page.waitForTimeout(2000);
                    console.log('✓ Confirmed regeneration');
                }
            }
            
            // Check for success message or error
            await page.waitForTimeout(1000);
            const bodyText = await page.textContent('body');
            if (bodyText.includes('success') || bodyText.includes('regenerated')) {
                console.log('✅ Invoice PDF regeneration successful!');
            } else if (bodyText.includes('error') || bodyText.includes('failed')) {
                console.error('❌ Invoice regeneration failed');
            }
        } else {
            console.log('⚠️  "Regenerate Invoice PDF" button not found');
        }

        // Check for monthly report regenerate button
        const regenerateMonthlyBtn = page.locator('button').filter({ hasText: /Verify.*Monthly.*Report|Regenerate.*Monthly/i });
        if (await regenerateMonthlyBtn.isVisible()) {
            console.log('✓ Found "Verify Monthly Report" button');
            
            // Click the button
            await regenerateMonthlyBtn.click();
            await page.waitForTimeout(500);
            
            // Check if confirmation dialog appears
            const dialogText = await page.textContent('body');
            if (dialogText.includes('verify') || dialogText.includes('confirm')) {
                console.log('✓ Confirmation dialog appeared');
                
                // Confirm the action
                const confirmBtn = page.locator('button').filter({ hasText: /yes|confirm|ok/i }).first();
                if (await confirmBtn.isVisible()) {
                    await confirmBtn.click();
                    await page.waitForTimeout(2000);
                    console.log('✓ Confirmed verification');
                }
            }
            
            // Check for success message
            await page.waitForTimeout(1000);
            const bodyText = await page.textContent('body');
            if (bodyText.includes('success') || bodyText.includes('ready') || bodyText.includes('verified')) {
                console.log('✅ Monthly report verification successful!');
            } else if (bodyText.includes('error') || bodyText.includes('failed')) {
                console.error('❌ Monthly report verification failed');
            }
        } else {
            console.log('⚠️  "Verify Monthly Report" button not visible (invoice may not be Monthly type)');
        }

        console.log('\n✅ PDF regeneration test completed');

    } catch (error) {
        console.error('❌ Test failed:', error.message);
        throw error;
    } finally {
        await page.waitForTimeout(2000);
        await browser.close();
    }
}

// Run test
testPdfRegeneration().catch(console.error);
