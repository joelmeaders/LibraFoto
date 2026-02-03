import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { describe, it, expect } from "vitest";
import { AppComponent } from "./app.component";

describe("AppComponent", () => {
  it("renders the toolbar title", () => {
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])],
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const spans = Array.from(
      fixture.nativeElement.querySelectorAll("mat-toolbar span"),
    );
    const titleSpan = spans.find((span) =>
      (span as HTMLElement).textContent?.includes("LibraFoto Admin"),
    ) as HTMLElement | undefined;

    expect(titleSpan).toBeTruthy();
  });
});
