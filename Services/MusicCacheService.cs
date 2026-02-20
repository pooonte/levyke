using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.FileProperties;
using System.Runtime.Serialization.Json;
using System.Text;
using levyke.Models;

namespace levyke.Services
{
    public static class MusicCacheService
    {
        private static readonly string CacheFile = "music_cache.json";
        private static readonly string CacheFolder = ApplicationData.Current.LocalFolder.Path;
        private static readonly string CachePath = Path.Combine(CacheFolder, CacheFile);

        // ===== 1. СОХРАНЕНИЕ КЭША =====
        public static async Task SaveCacheAsync(List<CachedTrack> tracks)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<CachedTrack>));

                using (var stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, tracks);
                    stream.Position = 0;

                    using (var reader = new StreamReader(stream))
                    {
                        string json = await reader.ReadToEndAsync();

                        var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                            CacheFile, CreationCollisionOption.ReplaceExisting);

                        await FileIO.WriteTextAsync(file, json);

                        System.Diagnostics.Debug.WriteLine($"✅ Кэш сохранён: {tracks.Count} треков");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка сохранения кэша: {ex.Message}");
            }
        }

        // ===== 2. ЗАГРУЗКА КЭША =====
        public static async Task<List<CachedTrack>> LoadCacheAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(CacheFile);
                string json = await FileIO.ReadTextAsync(file);

                if (!string.IsNullOrEmpty(json))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<CachedTrack>));

                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var tracks = serializer.ReadObject(stream) as List<CachedTrack>;
                        System.Diagnostics.Debug.WriteLine($"✅ Кэш загружен: {tracks?.Count ?? 0} треков");
                        return tracks ?? new List<CachedTrack>();
                    }
                }
            }
            catch (FileNotFoundException)
            {
                System.Diagnostics.Debug.WriteLine("📭 Кэш не найден (первый запуск)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки кэша: {ex.Message}");
            }

            return new List<CachedTrack>();
        }

        // ===== 3. ПРОВЕРКА НАЛИЧИЯ КЭША =====
        public static async Task<bool> HasCacheAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(CacheFile);
                var props = await file.GetBasicPropertiesAsync();
                return props.Size > 0;
            }
            catch
            {
                return false;
            }
        }

        // ===== 4. ПОЛНОЕ СКАНИРОВАНИЕ БИБЛИОТЕКИ =====
        public static async Task<List<CachedTrack>> FullScanAsync()
        {
            System.Diagnostics.Debug.WriteLine("🔍 Начинаем полное сканирование...");

            var tracks = new List<CachedTrack>();

            try
            {
                var musicFolder = KnownFolders.MusicLibrary;
                var queryOptions = new QueryOptions
                {
                    FolderDepth = FolderDepth.Deep,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable // используем индекс для скорости
                };

                queryOptions.FileTypeFilter.Add(".mp3");
                queryOptions.FileTypeFilter.Add(".flac");
                queryOptions.FileTypeFilter.Add(".m4a");
                queryOptions.FileTypeFilter.Add(".wma");
                queryOptions.FileTypeFilter.Add(".wav");

                var query = musicFolder.CreateFileQueryWithOptions(queryOptions);
                var files = await query.GetFilesAsync();

                System.Diagnostics.Debug.WriteLine($"📁 Найдено файлов: {files.Count}");

                int processed = 0;
                int total = files.Count;

                foreach (var file in files)
                {
                    try
                    {
                        var props = await file.Properties.GetMusicPropertiesAsync();
                        var basicProps = await file.GetBasicPropertiesAsync();

                        var track = new CachedTrack
                        {
                            FilePath = file.Path,
                            Title = string.IsNullOrEmpty(props.Title) ? file.DisplayName : props.Title,
                            Artist = string.IsNullOrEmpty(props.Artist) ? "Неизвестный исполнитель" : props.Artist,
                            Album = string.IsNullOrEmpty(props.Album) ? "Неизвестный альбом" : props.Album,
                            Duration = props.Duration.ToString(),
                            LastModified = basicProps.DateModified.DateTime,
                            LastScanned = DateTime.Now
                        };

                        tracks.Add(track);
                        processed++;

                        if (processed % 100 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"⏳ Обработано {processed}/{total}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Ошибка с файлом {file.Name}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Сканирование завершено: {tracks.Count} треков");

                // Сохраняем в кэш
                await SaveCacheAsync(tracks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка сканирования: {ex.Message}");
            }

            return tracks;
        }

        // ===== 5. БЫСТРАЯ ПРОВЕРКА НОВЫХ ФАЙЛОВ =====
        public static async Task<List<CachedTrack>> QuickCheckAsync(List<CachedTrack> existingCache)
        {
            if (existingCache == null || existingCache.Count == 0)
                return await FullScanAsync();

            System.Diagnostics.Debug.WriteLine("🔎 Быстрая проверка новых файлов...");

            var newTracks = new List<CachedTrack>();
            var deletedPaths = new HashSet<string>(existingCache.Select(t => t.FilePath));

            try
            {
                var musicFolder = KnownFolders.MusicLibrary;
                var queryOptions = new QueryOptions
                {
                    FolderDepth = FolderDepth.Deep,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable
                };

                queryOptions.FileTypeFilter.Add(".mp3");
                queryOptions.FileTypeFilter.Add(".flac");

                var query = musicFolder.CreateFileQueryWithOptions(queryOptions);
                var files = await query.GetFilesAsync();

                // Проверяем новые файлы
                foreach (var file in files)
                {
                    if (!deletedPaths.Contains(file.Path))
                    {
                        // Новый файл!
                        try
                        {
                            var props = await file.Properties.GetMusicPropertiesAsync();
                            var basicProps = await file.GetBasicPropertiesAsync();

                            var track = new CachedTrack
                            {
                                FilePath = file.Path,
                                Title = string.IsNullOrEmpty(props.Title) ? file.DisplayName : props.Title,
                                Artist = string.IsNullOrEmpty(props.Artist) ? "Неизвестный исполнитель" : props.Artist,
                                Album = string.IsNullOrEmpty(props.Album) ? "Неизвестный альбом" : props.Album,
                                Duration = props.Duration.ToString(),
                                LastModified = basicProps.DateModified.DateTime,
                                LastScanned = DateTime.Now
                            };

                            newTracks.Add(track);
                            System.Diagnostics.Debug.WriteLine($"➕ Новый файл: {file.Name}");
                        }
                        catch { }
                    }

                    deletedPaths.Remove(file.Path);
                }

                // Удалённые файлы
                int deletedCount = deletedPaths.Count;
                if (deletedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"➖ Удалено файлов: {deletedCount}");
                }

                // Объединяем: существующие + новые - удалённые
                var updatedCache = existingCache
                    .Where(t => !deletedPaths.Contains(t.FilePath))
                    .Concat(newTracks)
                    .ToList();

                if (newTracks.Count > 0 || deletedCount > 0)
                {
                    await SaveCacheAsync(updatedCache);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Быстрая проверка: +{newTracks.Count}, -{deletedCount}");

                return updatedCache;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка быстрой проверки: {ex.Message}");
                return existingCache;
            }
        }
    }
}