import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideTransloco } from '@jsverse/transloco';
import { AppComponent } from './app/app.component';
import { jwtInterceptor } from './app/core/interceptors/jwt.interceptor';
import { errorInterceptor } from './app/core/interceptors/error.interceptor';
import { AppTranslocoLoader } from './app/core/i18n/transloco.loader';

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor])),
    ...provideTransloco({
      config: { availableLangs: ['vi', 'en'], defaultLang: 'vi', reRenderOnLangChange: true, prodMode: true },
      loader: AppTranslocoLoader
    })
  ]
}).catch(console.error);
