import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect } from "vitest";
import { ActivatedRoute } from "@angular/router";
import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting } from "@angular/common/http/testing";
import { OAuthCallbackComponent } from "./oauth-callback.component";

describe("OAuthCallbackComponent", () => {
  it("shows an error when the callback returns an error param", () => {
    const routeStub = {
      queryParams: of({ error: "access_denied" }),
    } as Partial<ActivatedRoute>;

    TestBed.configureTestingModule({
      imports: [OAuthCallbackComponent],
      providers: [
        { provide: ActivatedRoute, useValue: routeStub },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    const fixture = TestBed.createComponent(OAuthCallbackComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.status).toBe("error");
    expect(component.errorMessage).toContain("access_denied");
  });
});
