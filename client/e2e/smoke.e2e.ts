import { Page, expect, test } from '@playwright/test';

/** Seeded by DevelopmentDataSeeder (Development environment, docker compose). */
const HR_EMAIL = 'hr@payroll.local';
const SEEDED_EMPLOYEE = 'Ali Hassan';

const languages = {
  en: {
    dir: 'ltr',
    employeesTitle: 'Employees · Payroll',
    payslipTitle: 'Payslip · Payroll',
    payItemsTitle: 'Pay items · Payroll',
    policiesTitle: 'Payroll policies · Payroll',
  },
  ar: {
    dir: 'rtl',
    employeesTitle: 'الموظفون · الرواتب',
    payslipTitle: 'الراتب · الرواتب',
    payItemsTitle: 'أنواع البنود · الرواتب',
    policiesTitle: 'سياسات الرواتب · الرواتب',
  },
} as const;

async function signInAsHr(page: Page, language: keyof typeof languages): Promise<void> {
  const password = process.env['E2E_HR_PASSWORD'];
  expect(password, 'Set E2E_HR_PASSWORD to Seed__HrPassword from .env').toBeTruthy();

  await page.goto(`/${language}/login`);
  await expect(page.locator('html')).toHaveAttribute('dir', languages[language].dir);
  await page.locator('#email').fill(HR_EMAIL);
  await page.locator('#password').fill(password ?? '');
  await page.locator('form button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`/${language}/home$`));
}

for (const language of Object.keys(languages) as (keyof typeof languages)[]) {
  test(`HR signs in and opens the employee list and a payslip (/${language}/)`, async ({ page }) => {
    const texts = languages[language];
    await signInAsHr(page, language);

    await page.locator('nav a[href$="/employees"]').click();
    await expect(page).toHaveURL(new RegExp(`/${language}/employees`));
    await expect(page).toHaveTitle(texts.employeesTitle);

    await page.locator('#search').fill('Ali');
    await page.locator('form.toolbar button[type=submit]').click();
    const row = page.locator('tbody tr', { hasText: SEEDED_EMPLOYEE });
    await expect(row).toBeVisible();

    await row.locator('a[href*="/payslip"]').click();
    await expect(page).toHaveURL(new RegExp(`/${language}/employees/\\d+/payslip`));
    await expect(page).toHaveTitle(texts.payslipTitle);
    await expect(page.locator('article.payslip')).toContainText(SEEDED_EMPLOYEE);
  });
}

for (const language of Object.keys(languages) as (keyof typeof languages)[]) {
  test(`HR opens the pay items and payroll policies screens (/${language}/)`, async ({ page }) => {
    const texts = languages[language];
    await signInAsHr(page, language);

    // Read-only: no data is created or changed.
    await page.locator('nav a[href$="/pay-items"]').click();
    await expect(page).toHaveURL(new RegExp(`/${language}/pay-items`));
    await expect(page).toHaveTitle(texts.payItemsTitle);
    await expect(page.locator('tbody tr').first()).toBeVisible();

    await page.locator('nav a[href$="/payroll-policies"]').click();
    await expect(page).toHaveURL(new RegExp(`/${language}/payroll-policies`));
    await expect(page).toHaveTitle(texts.policiesTitle);
    await expect(page.locator('.scheme-tabs button').first()).toBeVisible();
  });
}
