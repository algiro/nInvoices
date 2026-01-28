// PDF Generation Test Script
// Tests that the PDF generation produces high-quality output matching the HTML template

import https from 'https';
import http from 'http';
import fs from 'fs';
import path from 'path';

const API_BASE_URL = 'http://localhost:5297/api';
const OUTPUT_DIR = './Docs/Issues';

// Helper to make HTTP requests
function makeRequest(url, options = {}) {
  return new Promise((resolve, reject) => {
    const urlObj = new URL(url);
    const protocol = urlObj.protocol === 'https:' ? https : http;
    
    const req = protocol.request(url, options, (res) => {
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

// Test: Regenerate Invoice PDF
async function testRegenerateInvoice(invoiceId) {
  console.log(`\n🔄 Testing: Regenerate Invoice #${invoiceId}`);
  
  try {
    const response = await makeRequest(
      `${API_BASE_URL}/invoices/${invoiceId}/regenerate`,
      { method: 'POST' }
    );
    
    const result = JSON.parse(response.body.toString());
    console.log(`✅ Regenerate successful: ${result.message}`);
    return true;
  } catch (error) {
    console.error(`❌ Regenerate failed: ${error.message}`);
    return false;
  }
}

// Test: Download Invoice PDF
async function testDownloadInvoicePdf(invoiceId) {
  console.log(`\n📥 Testing: Download Invoice PDF #${invoiceId}`);
  
  try {
    const response = await makeRequest(`${API_BASE_URL}/invoices/${invoiceId}/pdf`);
    
    const filename = `Invoice-${invoiceId}-puppeteer-test.pdf`;
    const filepath = path.join(OUTPUT_DIR, filename);
    
    // Ensure directory exists
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
    
    // Save PDF
    fs.writeFileSync(filepath, response.body);
    
    const sizeKB = (response.body.length / 1024).toFixed(2);
    console.log(`✅ PDF downloaded successfully`);
    console.log(`   Size: ${sizeKB} KB`);
    console.log(`   Saved to: ${filepath}`);
    
    return { success: true, filepath, size: response.body.length };
  } catch (error) {
    console.error(`❌ Download failed: ${error.message}`);
    return { success: false, error: error.message };
  }
}

// Test: Verify PDF contains expected content
async function verifyPdfContent(invoiceId) {
  console.log(`\n🔍 Testing: PDF Content Verification for Invoice #${invoiceId}`);
  
  // Download the PDF first
  const result = await testDownloadInvoicePdf(invoiceId);
  
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
  
  console.log(`✅ PDF validation passed`);
  console.log(`   Valid PDF format (${result.size} bytes)`);
  
  return true;
}

// Main test execution
async function runTests() {
  console.log('🧪 Starting PDF Generation Tests with Puppeteer Sharp\n');
  console.log('================================================\n');
  
  const INVOICE_ID = 24; // Test invoice ID
  let allPassed = true;
  
  // Test 1: Regenerate invoice
  const regenerateSuccess = await testRegenerateInvoice(INVOICE_ID);
  allPassed = allPassed && regenerateSuccess;
  
  // Wait a bit for generation to complete
  await new Promise(resolve => setTimeout(resolve, 3000));
  
  // Test 2: Download and verify PDF
  const verifySuccess = await verifyPdfContent(INVOICE_ID);
  allPassed = allPassed && verifySuccess;
  
  // Summary
  console.log('\n================================================');
  if (allPassed) {
    console.log('✅ All tests PASSED!\n');
    console.log('Next step: Manually review the generated PDF to verify:');
    console.log('  - Gradient header renders correctly');
    console.log('  - Tables are properly formatted');
    console.log('  - Colors and styling match the HTML template');
    console.log('  - Text is clear and professional');
    process.exit(0);
  } else {
    console.log('❌ Some tests FAILED\n');
    process.exit(1);
  }
}

// Run tests
runTests().catch(error => {
  console.error('💥 Test suite crashed:', error);
  process.exit(1);
});
