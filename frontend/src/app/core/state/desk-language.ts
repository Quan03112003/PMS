import { signal } from '@angular/core';

export const deskLanguage = signal<'vi' | 'en'>(localStorage.getItem('pms_lang') === 'en' ? 'en' : 'vi');
