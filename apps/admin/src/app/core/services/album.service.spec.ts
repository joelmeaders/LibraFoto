import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { AlbumService } from "./album.service";
import { AlbumDto, PagedResult, PhotoListDto, MediaType } from "../models";

describe("AlbumService", () => {
  let service: AlbumService;
  let httpMock: HttpTestingController;
  const baseUrl = "";

  const mockAlbum: AlbumDto = {
    id: 1,
    name: "Test Album",
    description: "Test Description",
    coverPhotoId: 1,
    coverPhotoThumbnail: "thumb/cover.jpg",
    dateCreated: "2025-01-01T00:00:00Z",
    sortOrder: 0,
    photoCount: 10,
  };

  const mockPhotoList: PhotoListDto = {
    id: 1,
    filename: "test.jpg",
    thumbnailPath: "thumb/test.jpg",
    mediaType: MediaType.Photo,
    dateTaken: "2025-01-01T12:00:00Z",
    dateAdded: "2025-01-01T12:00:00Z",
    width: 1920,
    height: 1080,
    location: null,
    albumCount: 1,
    tagCount: 0,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AlbumService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AlbumService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe("Initial state", () => {
    it("should start with empty albums array", () => {
      expect(service.albums()).toEqual([]);
    });

    it("should start with no selected album", () => {
      expect(service.selectedAlbum()).toBeNull();
    });

    it("should start with isLoading false", () => {
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getAlbums", () => {
    it("should fetch all albums", () => {
      const albums = [mockAlbum];

      service.getAlbums().subscribe((result) => {
        expect(result).toEqual(albums);
        expect(service.albums()).toEqual(albums);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/albums`);
      expect(req.request.method).toBe("GET");
      req.flush(albums);
    });

    it("should set loading state during fetch", () => {
      expect(service.isLoading()).toBe(false);

      service.getAlbums().subscribe();
      expect(service.isLoading()).toBe(true);

      httpMock.expectOne(`${baseUrl}/api/admin/albums`).flush([]);
      expect(service.isLoading()).toBe(false);
    });
  });

  describe("getAlbum", () => {
    it("should fetch a single album and set selectedAlbum", () => {
      service.getAlbum(1).subscribe((result) => {
        expect(result).toEqual(mockAlbum);
        expect(service.selectedAlbum()).toEqual(mockAlbum);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/albums/1`);
      expect(req.request.method).toBe("GET");
      req.flush(mockAlbum);
    });
  });

  describe("createAlbum", () => {
    it("should create album and add to albums list", () => {
      const createRequest = {
        name: "New Album",
        description: "New Description",
      };
      const createdAlbum = { ...mockAlbum, id: 2, name: "New Album" };

      service.createAlbum(createRequest).subscribe((result) => {
        expect(result).toEqual(createdAlbum);
        expect(service.albums()).toContainEqual(createdAlbum);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/albums`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(createRequest);
      req.flush(createdAlbum);
    });
  });

  describe("updateAlbum", () => {
    it("should update album and update albums list", () => {
      // First load albums
      service.getAlbums().subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums`).flush([mockAlbum]);

      const updateRequest = { name: "Updated Album" };
      const updatedAlbum = { ...mockAlbum, name: "Updated Album" };

      service.updateAlbum(1, updateRequest).subscribe((result) => {
        expect(result).toEqual(updatedAlbum);
        expect(service.albums().find((a) => a.id === 1)?.name).toBe(
          "Updated Album",
        );
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/albums/1`);
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(updateRequest);
      req.flush(updatedAlbum);
    });

    it("should update selectedAlbum if it matches", () => {
      // First select the album
      service.getAlbum(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums/1`).flush(mockAlbum);

      const updateRequest = { name: "Updated Album" };
      const updatedAlbum = { ...mockAlbum, name: "Updated Album" };

      service.updateAlbum(1, updateRequest).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums/1`).flush(updatedAlbum);

      expect(service.selectedAlbum()?.name).toBe("Updated Album");
    });
  });

  describe("deleteAlbum", () => {
    it("should delete album and remove from albums list", () => {
      // First load albums
      service.getAlbums().subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums`).flush([mockAlbum]);

      expect(service.albums().length).toBe(1);

      service.deleteAlbum(1).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/api/admin/albums/1`);
      expect(req.request.method).toBe("DELETE");
      req.flush(null);

      expect(service.albums().length).toBe(0);
    });

    it("should clear selectedAlbum if deleted album was selected", () => {
      // First select an album
      service.getAlbum(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums/1`).flush(mockAlbum);

      expect(service.selectedAlbum()).not.toBeNull();

      service.deleteAlbum(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums/1`).flush(null);

      expect(service.selectedAlbum()).toBeNull();
    });
  });

  describe("addPhotosToAlbum", () => {
    it("should add photos to album", () => {
      const request = { photoIds: [1, 2, 3] };
      const bulkResult = { successCount: 3, failedCount: 0, errors: [] };

      service.addPhotosToAlbum(1, request).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(`${baseUrl}/api/admin/albums/1/photos`);
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(request);
      req.flush(bulkResult);
    });
  });

  describe("removePhotosFromAlbum", () => {
    it("should remove photos from album", () => {
      const request = { photoIds: [1, 2] };
      const bulkResult = { successCount: 2, failedCount: 0, errors: [] };

      service.removePhotosFromAlbum(1, request).subscribe((result) => {
        expect(result).toEqual(bulkResult);
      });

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/albums/1/photos/remove`,
      );
      expect(req.request.method).toBe("POST");
      expect(req.request.body).toEqual(request);
      req.flush(bulkResult);
    });
  });

  describe("reorderPhotos", () => {
    it("should reorder photos in album", () => {
      const request = {
        photoOrders: [
          { photoId: 1, sortOrder: 0 },
          { photoId: 2, sortOrder: 1 },
        ],
      };

      service.reorderPhotos(1, request).subscribe();

      const req = httpMock.expectOne(
        `${baseUrl}/api/admin/albums/1/photos/reorder`,
      );
      expect(req.request.method).toBe("PUT");
      expect(req.request.body).toEqual(request);
      req.flush(null);
    });
  });

  describe("getAlbumPhotos", () => {
    it("should fetch paginated photos for album", () => {
      const pagedResult: PagedResult<PhotoListDto> = {
        data: [mockPhotoList],
        pagination: { page: 1, pageSize: 50, totalItems: 1, totalPages: 1 },
      };

      service.getAlbumPhotos(1).subscribe((result) => {
        expect(result).toEqual(pagedResult);
      });

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/albums/1/photos`,
      );
      expect(req.request.params.get("page")).toBe("1");
      expect(req.request.params.get("pageSize")).toBe("50");
      req.flush(pagedResult);
    });

    it("should accept custom page and pageSize", () => {
      service.getAlbumPhotos(1, 2, 25).subscribe();

      const req = httpMock.expectOne(
        (r) => r.url === `${baseUrl}/api/admin/albums/1/photos`,
      );
      expect(req.request.params.get("page")).toBe("2");
      expect(req.request.params.get("pageSize")).toBe("25");
      req.flush({ data: [], pagination: {} });
    });
  });

  describe("clearSelectedAlbum", () => {
    it("should clear the selected album", () => {
      // First select an album
      service.getAlbum(1).subscribe();
      httpMock.expectOne(`${baseUrl}/api/admin/albums/1`).flush(mockAlbum);

      expect(service.selectedAlbum()).not.toBeNull();

      service.clearSelectedAlbum();
      expect(service.selectedAlbum()).toBeNull();
    });
  });

  describe("getCoverThumbnailUrl", () => {
    it("should generate cover thumbnail URL when coverPhotoId exists", () => {
      const url = service.getCoverThumbnailUrl(mockAlbum);
      expect(url).toBe(
        `${baseUrl}/api/media/thumbnails/${mockAlbum.coverPhotoId}`,
      );
    });

    it("should return null when coverPhotoId is missing", () => {
      const albumWithoutCover: AlbumDto = {
        ...mockAlbum,
        coverPhotoId: null,
      };
      const url = service.getCoverThumbnailUrl(albumWithoutCover);
      expect(url).toBeNull();
    });
  });
});
