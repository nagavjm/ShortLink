import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

// Developer utility: inspect Redis cache directly from Browser DevTools Console (F12)
(window as any).checkCache = async (shortCode: string = '000001') => {
  const code = (shortCode || '000001').trim();
  try {
    const res = await fetch(`http://localhost:5050/api/urls/cache/${code}`);
    const data = await res.json();
    console.group(`🔍 Redis Cache Inspector for Short Code: [${code}]`);
    console.table(data);
    console.log('Raw Payload:', data);
    console.groupEnd();
    return data;
  } catch (err) {
    console.error(`Failed to inspect Redis cache for '${code}':`, err);
  }
};

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
