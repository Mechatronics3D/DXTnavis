using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Autodesk.Navisworks.Api.Plugins;

namespace DXTnavis
{
    /// <summary>
    /// Navisworks 속성 확인기 플러그인 메인 클래스
    /// 실시간으로 선택된 객체의 속성을 표시하고 내보내기 기능을 제공합니다.
    /// </summary>
    [Plugin("DXTnavis.DX", "YourCompany", DisplayName = "DXTnavis 속성 확인기", ToolTip = "선택된 객체의 속성을 실시간으로 표시합니다")]
    [AddInPlugin(AddInLocation.AddIn, LoadForCanExecute = true)]
    public class DX : AddInPlugin
    {
        /// <summary>
        /// 싱글톤 인스턴스 - 창이 중복 생성되지 않도록 관리
        /// </summary>
        private static Views.DXwindow _windowInstance;

        /// <summary>
        /// AssemblyResolve 등록 여부
        /// </summary>
        private static bool _resolverRegistered;

        /// <summary>
        /// 플러그인 폴더에서 종속 어셈블리를 찾는 핸들러 등록
        /// Navisworks CLR은 플러그인 하위폴더를 자동으로 probing하지 않으므로 필요
        /// </summary>
        private static void EnsureAssemblyResolver()
        {
            if (_resolverRegistered) return;
            _resolverRegistered = true;

            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                string assemblyPath = Path.Combine(pluginDir, assemblyName);

                if (File.Exists(assemblyPath))
                    return Assembly.LoadFrom(assemblyPath);

                return null;
            };
        }

        /// <summary>
        /// 플러그인 실행 메서드
        /// 리본 버튼 클릭 시 호출됩니다.
        /// </summary>
        /// <param name="parameters">실행 파라미터</param>
        /// <returns>실행 결과 코드</returns>
        public override int Execute(params string[] parameters)
        {
            EnsureAssemblyResolver();
            try
            {
                // DEBUG: 개발 중에는 항상 새 창 생성 (UI 업데이트 테스트용)
                // 프로덕션에서는 싱글톤 패턴 사용 권장
                #if DEBUG
                // 기존 창이 있으면 닫기
                if (_windowInstance != null && _windowInstance.IsLoaded)
                {
                    _windowInstance.Close();
                    _windowInstance = null;
                }
                #else
                // 싱글톤 패턴: 창이 이미 열려있으면 활성화만 수행
                if (_windowInstance != null && _windowInstance.IsLoaded)
                {
                    // 최소화되어 있으면 복원
                    if (_windowInstance.WindowState == WindowState.Minimized)
                    {
                        _windowInstance.WindowState = WindowState.Normal;
                    }

                    // 창을 맨 앞으로 가져오고 포커스
                    _windowInstance.Activate();
                    _windowInstance.Focus();
                    return 0; // 기존 창 활성화 성공
                }
                #endif

                // 새 창 인스턴스 생성 및 표시
                _windowInstance = new Views.DXwindow();

                // 창이 닫힐 때 인스턴스 정리
                _windowInstance.Closed += (s, e) => _windowInstance = null;

                // 모달리스 창으로 표시 (Navisworks 작업 계속 가능)
                _windowInstance.Show();

                return 0; // 성공
            }
            catch (Exception ex)
            {
                // 오류 발생 시 사용자에게 알림
                MessageBox.Show(
                    $"플러그인 실행 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "DX 속성 확인기 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return 1; // 실패
            }
        }
    }
}
