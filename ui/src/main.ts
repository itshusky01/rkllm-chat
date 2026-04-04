import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';

bootstrapApplication(App,  {
  providers: [
    provideBrowserGlobalErrorListeners(),
  ]
} satisfies ApplicationConfig)
  .catch((err) => console.error(err));
