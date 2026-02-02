import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { DashboardComponent } from "./dashboard.component";
import { PhotoService } from "../../core/services/photo.service";
import { AlbumService } from "../../core/services/album.service";
import { TagService } from "../../core/services/tag.service";
import { StorageService } from "../../core/services/storage.service";

describe("DashboardComponent", () => {
  it("loads stats and renders counts", () => {
    const photoServiceStub = {
      getPhotoCount: vi.fn(() => of({ count: 12 })),
    } as Partial<PhotoService>;

    const albumServiceStub = {
      getAlbums: vi.fn(() => of([])),
    } as Partial<AlbumService>;

    const tagServiceStub = {
      getTags: vi.fn(() => of([])),
    } as Partial<TagService>;

    const storageServiceStub = {
      getProviders: vi.fn(() => of([])),
    } as Partial<StorageService>;

    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: PhotoService, useValue: photoServiceStub },
        { provide: AlbumService, useValue: albumServiceStub },
        { provide: TagService, useValue: tagServiceStub },
        { provide: StorageService, useValue: storageServiceStub },
      ],
    });

    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.stats().photoCount).toBe(12);
    expect(fixture.componentInstance.isLoading()).toBe(false);
  });
});
