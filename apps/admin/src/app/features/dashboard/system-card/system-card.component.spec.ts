import { ComponentFixture, TestBed } from "@angular/core/testing";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { provideHttpClient } from "@angular/common/http";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { SystemCardComponent } from "./system-card.component";
import { SystemService } from "../../../core/services/system.service";
import { SystemInfoResponse } from "../../../core/models";

describe("SystemCardComponent", () => {
  let component: SystemCardComponent;
  let fixture: ComponentFixture<SystemCardComponent>;
  let httpMock: HttpTestingController;
  const baseUrl = "";

  const mockSystemInfo: SystemInfoResponse = {
    version: "1.0.0",
    commitHash: "abc1234",
    updateAvailable: false,
    latestVersion: null,
    commitsBehind: 0,
    changelog: null,
    uptime: "01:30:00",
    isDocker: false,
    environment: "Development",
  };

  const mockSystemInfoWithUpdate: SystemInfoResponse = {
    version: "1.0.0",
    commitHash: "abc1234",
    updateAvailable: true,
    latestVersion: "1.1.0",
    commitsBehind: 5,
    changelog: ["Added feature A", "Fixed bug B"],
    uptime: "02:45:30",
    isDocker: true,
    environment: "Production",
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SystemCardComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        SystemService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SystemCardComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it("should create", () => {
    fixture.detectChanges(); // Trigger ngOnInit
    expect(component).toBeTruthy();

    // Flush the initial system info request
    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfo);
  });

  it("should load system info on init", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    expect(req.request.method).toBe("GET");
    req.flush(mockSystemInfo);

    expect(component.systemInfo()).toEqual(mockSystemInfo);
    expect(component.isLoading()).toBe(false);
  });

  it("should display version info", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfo);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("1.0.0");
    expect(compiled.textContent).toContain("abc1234");
  });

  it("should show loading state initially", () => {
    // Check loading state before detectChanges (no HTTP call yet)
    expect(component.isLoading()).toBe(true);

    fixture.detectChanges(); // Trigger ngOnInit

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfo);
  });

  it("should handle error state", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.error(new ProgressEvent("error"), {
      status: 500,
      statusText: "Server Error",
    });

    expect(component.error()).not.toBeNull();
    expect(component.isLoading()).toBe(false);
  });

  it("should show update available badge when update is available", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfoWithUpdate);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("Update Available");
    expect(compiled.textContent).toContain("1.1.0");
  });

  it("should show commits behind when update is available", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfoWithUpdate);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("5 commits behind");
  });

  it("should have update button disabled when no update available", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfo);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const updateButton = compiled.querySelector('button[color="warn"]');

    // Update button should not exist when no update available
    expect(updateButton).toBeNull();
  });

  it("should have update button enabled when update is available", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfoWithUpdate);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const updateButton = compiled.querySelector('button[color="warn"]');

    expect(updateButton).not.toBeNull();
    expect(updateButton?.hasAttribute("disabled")).toBe(false);
  });

  it("should format uptime correctly", () => {
    expect(component.formatUptime("01:30:00")).toBe("1h 30m");
    expect(component.formatUptime("00:45:00")).toBe("45m");
    expect(component.formatUptime("1.02:30:00")).toBe("1d 2h 30m");
    expect(component.formatUptime("00:00:00")).toBe("0m");

    // Trigger detectChanges followed by flushing the initial request
    fixture.detectChanges();
    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfo);
  });

  it("should call checkUpdates when button is clicked", () => {
    fixture.detectChanges();

    const req1 = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req1.flush(mockSystemInfo);
    fixture.detectChanges();

    component.checkUpdates();

    const req2 = httpMock.expectOne(
      `${baseUrl}/api/admin/system/updates/check`,
    );
    expect(req2.request.method).toBe("POST");
    req2.flush({
      updateAvailable: false,
      currentVersion: "1.0.0",
      latestVersion: "1.0.0",
      commitsBehind: 0,
      changelog: [],
      error: null,
      checkedAt: new Date().toISOString(),
    });

    // After update check, system info is reloaded
    const req3 = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req3.flush(mockSystemInfo);
  });

  it("should display environment badge", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfo);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("Development");
  });

  it("should show Docker indicator when running in Docker", () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/api/admin/system/info`);
    req.flush(mockSystemInfoWithUpdate);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain("Docker");
  });
});
