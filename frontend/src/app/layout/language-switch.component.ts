import { Component, OnInit } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { deskLanguage } from '../core/state/desk-language';

@Component({
  selector: 'app-language-switch',
  standalone: true,
  template: `
    <span class="live-dot"><i></i><span>{{ label }}</span></span>
    <button type="button" class="icon-btn" (click)="toggle()">{{ lang() === 'vi' ? 'VI' : 'EN' }}</button>
  `
})
export class LanguageSwitchComponent implements OnInit {
  lang = deskLanguage;
  label = 'Live demo';
  constructor(private transloco: TranslocoService) {}

  ngOnInit(): void {
    this.transloco.setActiveLang(this.lang());
    this.transloco.selectTranslate('shell.live').subscribe(text => {
      if (text && text !== 'shell.live') this.label = text;
    });
  }

  toggle(): void {
    const next = this.lang() === 'vi' ? 'en' : 'vi';
    this.lang.set(next);
    localStorage.setItem('pms_lang', next);
    this.transloco.setActiveLang(next);
  }
}
