import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from './api-base-url';
import { HealthProbe } from './catalog-diagnosis';
import { readAttachmentFileName } from './content-disposition';
import {
  GeneratedTemplate,
  ProblemDetails,
  TemplateOptionsCatalog,
  TemplateRequest,
} from './template-options.model';

/**
 * The only door to the generator API.
 *
 * It transports the catalog and the generated file; it does not interpret
 * option values and does not carry a copy of any compatibility rule. Those
 * live in the catalog (see `constraints.ts`).
 */
@Injectable({ providedIn: 'root' })
export class TemplateCatalogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /api/template-options` — fields, labels, defaults and constraints. */
  loadCatalog(): Observable<TemplateOptionsCatalog> {
    return this.http.get<TemplateOptionsCatalog>(`${this.baseUrl}/api/template-options`);
  }

  /**
   * `GET /api/health` — the tie-breaker between "the API is down" and "that
   * route is not there". It never fails: a refused connection is an answer.
   */
  probeHealth(): Observable<HealthProbe> {
    return this.http
      .get(`${this.baseUrl}/api/health`, { observe: 'response', responseType: 'text' })
      .pipe(
        map((response) => ({ reached: true, status: response.status })),
        catchError((error: unknown) => {
          const status = error instanceof HttpErrorResponse ? error.status : 0;
          return of({ reached: status !== 0, status });
        }),
      );
  }

  /** `POST /api/templates` — the ZIP, with the file name the API chose. */
  generate(request: TemplateRequest): Observable<GeneratedTemplate> {
    return this.http
      .post(`${this.baseUrl}/api/templates`, request, {
        observe: 'response',
        responseType: 'blob',
      })
      .pipe(
        map((response) => ({
          fileName:
            readAttachmentFileName(response.headers.get('Content-Disposition')) ??
            `${request.projectName}.zip`,
          content: response.body ?? new Blob(),
        })),
      );
  }
}

/**
 * Turns a failed call into the `ProblemDetails` the API sent, when it sent one.
 *
 * A `blob` response type makes the error body arrive as a `Blob`, so the caller
 * gets `null` there and falls back to a generic message.
 */
export function toProblemDetails(error: unknown): ProblemDetails | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }

  const body = error.error;
  if (body && typeof body === 'object' && !(body instanceof Blob)) {
    return body as ProblemDetails;
  }

  return null;
}

/** Reads a `ProblemDetails` that arrived as a blob, as blob responses do. */
export async function readProblemDetailsBlob(error: unknown): Promise<ProblemDetails | null> {
  if (!(error instanceof HttpErrorResponse) || !(error.error instanceof Blob)) {
    return toProblemDetails(error);
  }

  try {
    return JSON.parse(await error.error.text()) as ProblemDetails;
  } catch {
    return null;
  }
}
