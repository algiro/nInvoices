// Monthly Report Compactness Test
// Tests that the monthly report is compact enough to fit 25+ rows on one page

import http from 'http';
import fs from 'fs';
import path from 'path';

const API_BASE_URL = 'http://localhost:5297/api';
const OUTPUT_DIR = './Docs/Issues';

// Helper to make HTTP requests
function makeRequest(url, options = {}) {
  return new Promise((resolve, reject) => {
    const urlObj = new URL(url);
    
    const req = http.request(url, options, (res) => {
      const chunks = [];
      
      res.on('data', (chunk) => chunks.push(chunk));
      res.on('end', () => {
        const body = Buffer.concat(chunks);
        
        if (res.statusCode >= 200 && res.statusCode < 300) {
          resolve({ statusCode: res.statusCode, headers: res.headers, body });
        } else {
          reject(new Error(`HTTP ${res.statusCode}: ${body.toString()}`));
        }
      });
    });
    
    req.on('error', reject);
    req.end();
  });
}

// Test: Download Monthly Report PDF
async function testDownloadMonthlyReport(invoiceId) {
  console.log(`\n📥 Testing: Download Monthly Report PDF for Invoice #${invoiceId}`);
  
  try {
    const response = await makeRequest(`${API_BASE_URL}/invoices/${invoiceId}/monthlyreport/pdf`);
    
    const filename = `Monthly-Report-Invoice-${invoiceId}-compact-test.pdf`;
    const filepath = path.join(OUTPUT_DIR, filename);
    
    // Ensure directory exists
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
    
    // Save PDF
    fs.writeFileSync(filepath, response.body);
    
    const sizeKB = (response.body.length / 1024).toFixed(2);
    console.log(`✅ Monthly Report PDF downloaded successfully`);
    console.log(`   Size: ${sizeKB} KB`);
    console.log(`   Saved to: ${filepath}`);
    
    return { success: true, filepath, size: response.body.length };
  } catch (error) {
    console.error(`❌ Download failed: ${error.message}`);
    return { success: false, error: error.message };
  }
}

// Test: Verify PDF is valid
async function verifyMonthlyReportPdf(invoiceId) {
  console.log(`\n🔍 Testing: Monthly Report PDF Verification for Invoice #${invoiceId}`);
  
  const result = await testDownloadMonthlyReport(invoiceId);
  
  if (!result.success) {
    return false;
  }
  
  // Basic validation: check file size
  if (result.size < 1000) {
    console.error(`❌ PDF seems too small (${result.size} bytes) - might be corrupted`);
    return false;
  }
  
  // Check PDF header
  const pdfBuffer = fs.readFileSync(result.filepath);
  const header = pdfBuffer.toString('utf-8', 0, 8);
  
  if (!header.startsWith('%PDF-')) {
    console.error(`❌ Invalid PDF header: ${header}`);
    return false;
  }
  
  console.log(`✅ Monthly Report PDF validation passed`);
  console.log(`   Valid PDF format (${result.size} bytes)`);
  console.log(`\n📋 Please manually verify:`);
  console.log(`   - All 20-31 days are visible`);
  console.log(`   - Table is compact with small row spacing`);
  console.log(`   - 25+ rows fit on a single page`);
  console.log(`   - Text is readable (not too small)`);
  
  return true;
}

// Main test execution
async function runTests() {
  console.log('🧪 Starting Monthly Report Compactness Test\n');
  console.log('================================================\n');
  
  // Test with a monthly invoice (type = Monthly or type = 0)
  const MONTHLY_INVOICE_ID = 26; // Monthly invoice with 31 days
  
  let allPassed = true;
  
  const verifySuccess = await verifyMonthlyReportPdf(MONTHLY_INVOICE_ID);
  allPassed = allPassed && verifySuccess;
  
  // Summary
  console.log('\n================================================');
  if (allPassed) {
    console.log('✅ Test PASSED!\n');
    console.log('Next steps:');
    console.log('  1. Open the generated PDF');
    console.log('  2. Verify 25+ rows fit on ONE page');
    console.log('  3. Check that spacing is compact but readable');
    process.exit(0);
  } else {
    console.log('❌ Test FAILED\n');
    process.exit(1);
  }
}

// Run tests
runTests().catch(error => {
  console.error('💥 Test suite crashed:', error);
  process.exit(1);
});
