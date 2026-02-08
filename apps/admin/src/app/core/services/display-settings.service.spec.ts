import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { DisplaySettingsService } from "./display-settings.service";
import {
  DisplaySettingsDto,
  TransitionType,
  SourceType,
  ImageFit,
} from "../models";

describe("DisplaySettingsService", () => {
  let service: DisplaySettingsService;
  let httpMock: HttpTestingController;
  const baseUrl = "";

  const mockSettings: DisplaySettingsDto = {
    id: 1,
    name: "Default",
    slideDuration: 30,
    transition: TransitionType.Fade,
    transitionDuration: 1000,
    sourceType: SourceType.All,
    sourceId: null,
    shuffle: true,
    imageFit: ImageFit.Contain,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DisplaySettingsService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(DisplaySettingsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("Initial state", () => {
    it("should start with no settings", () => {
      expect(service.settings()).toBeNull();
    });

    it("should start with empty allSettings array", () => {
      expect(service.allSettings()).toEqual([]);
    });

    it("should start with isLoading false", () => {
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getSettings", () => {
    it("should fetch current settings", () => {
      service.getSettings().subscribe((result) => {
        expect(result).toEqual(mockSettings);
        expect(service.settings()).toEqual(mockSettings);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings`);
      expect(req.request.method).toBe("GET");
      req.flush(mockSettings);
    });

    it("should set loading state during fetch", () => {
      expect(service.isLoading()).toBe(false);

      service.getSettings().subscribe();
      expect(service.isLoading()).toBe(true);

      httpMock.expectOne(`${baseUrl}/api/display/settings`).flush(mockSettings);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getAllSettings", () => {
    it("should fetch all settings configurations", () => {
      const allSettings = [
        mockSettings,
        { ...mockSettings, id: 2, name: "Night Mode" },
      ];

      service.getAllSettings().subscribe((result) => {
        expect(result).toEqual(allSettings);
        expect(service.allSettings()).toEqual(allSettings);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings/all`);
      expect(req.request.method).toBe("GET");
      req.flush(allSettings);
    });

    it("should set loading state during fetch", () => {
      expect(service.isLoading()).toBe(false);

      service.getAllSettings().subscribe();
      expect(service.isLoading()).toBe(true);

      httpMock.expectOne(`${baseUrl}/api/display/settings/all`).flush([]);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getSettingsById", () => {
    it("should fetch specific settings by ID", () => {
      service.getSettingsById(1).subscribe((result) => {
        expect(result).toEqual(mockSettings);
        expect(service.settings()).toEqual(mockSettings);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings/1`);
      expect(req.request.method).toBe("GET");
      req.flush(mockSettings);
    });
  });

  describe("updateSettings", () => {
    it("should update settings and set current settings", () => {
      const updateRequest = { slideDuration: 60, shuffle: true };
      const updatedSettings = {
        ...mockSettings,
        slideDuration: 60,
        shuffle: true,
      };

      service.updateSettings(1, updateRequest).subscribe((result) => {
        expect(result).toEqual(updatedSettings);
        expect(service.settings()).toEqual(updatedSettings);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings/1`);
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(updateRequest);
      req.flush(updatedSettings);
    });

    it("should update allSettings list if settings exists there", () => {
      // First load all settings
      service.getAllSettings().subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/display/settings/all`)
        .flush([mockSettings]);

      const updateRequest = { name: "Updated Default" };
      const updatedSettings = { ...mockSettings, name: "Updated Default" };

      service.updateSettings(1, updateRequest).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/display/settings/1`)
        .flush(updatedSettings);

      expect(service.allSettings().find((s) => s.id === 1)?.name).toBe(
        "Updated Default",
      );
    });
  });

  describe("createSettings", () => {
    it("should create new settings and add to allSettings", () => {
      const createRequest = {
        name: "Night Mode",
        slideDuration: 60,
        transition: TransitionType.Slide,
      };
      const createdSettings = { ...mockSettings, id: 2, name: "Night Mode" };

      service.createSettings(createRequest).subscribe((result) => {
        expect(result).toEqual(createdSettings);
        expect(service.allSettings()).toContainEqual(createdSettings);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(createRequest);
      req.flush(createdSettings);
    });
  });

  describe("deleteSettings", () => {
    it("should delete settings and remove from allSettings", () => {
      // First load all settings
      const settings = [
        mockSettings,
        { ...mockSettings, id: 2, name: "Other" },
      ];
      service.getAllSettings().subscribe();
      httpMock.expectOne(`${baseUrl}/api/display/settings/all`).flush(settings);

      expect(service.allSettings().length).toBe(2);

      service.deleteSettings(2).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings/2`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);

      expect(service.allSettings().length).toBe(1);
      expect(service.allSettings().find((s) => s.id === 2)).toBeUndefined();
    });

    it("should clear settings if deleted settings was current", () => {
      // First set current settings
      service.getSettings().subscribe();
      httpMock.expectOne(`${baseUrl}/api/display/settings`).flush(mockSettings);

      expect(service.settings()).not.toBeNull();

      service.deleteSettings(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/display/settings/1`).flush(null);

      expect(service.settings()).toBeNull();
    });
  });

  describe("setActiveSettings", () => {
    it("should activate settings configuration", () => {
      const activatedSettings = { ...mockSettings, isActive: true };

      service.setActiveSettings(1).subscribe((result) => {
        expect(result).toEqual(activatedSettings);
        expect(service.settings()).toEqual(activatedSettings);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/display/settings/1/activate`,
      );
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({});
      req.flush(activatedSettings);
    });
  });

  describe("resetToDefaults", () => {
    it("should reset settings to defaults", () => {
      const defaultSettings = {
        ...mockSettings,
        slideDuration: 30,
        transition: TransitionType.Fade,
      };

      service.resetToDefaults(1).subscribe((result) => {
        expect(result).toEqual(defaultSettings);
        expect(service.settings()).toEqual(defaultSettings);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/display/settings/1/reset`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual({});
      req.flush(defaultSettings);
    });

    it("should update allSettings list after reset", () => {
      // First load all settings
      const customSettings = { ...mockSettings, slideDuration: 120 };
      service.getAllSettings().subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/display/settings/all`)
        .flush([customSettings]);

      const defaultSettings = { ...mockSettings, slideDuration: 30 };

      service.resetToDefaults(1).subscribe();
      httpMock
        .expectOne(`${baseUrl}/api/display/settings/1/reset`)
        .flush(defaultSettings);

      expect(service.allSettings().find((s) => s.id === 1)?.slideDuration).toBe(
        30,
      );
    });
  });

  describe("clearSettings", () => {
    it("should clear the current settings", () => {
      // First set settings
      service.getSettings().subscribe();
      httpMock.expectOne(`${baseUrl}/api/display/settings`).flush(mockSettings);

      expect(service.settings()).not.toBeNull();

      service.clearSettings();
      expect(service.settings()).toBeNull();
    });
  });
});
