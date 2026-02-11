# Desktop Fab Launcher - Status

## ビルド方法 (dotnet)
```powershell
cd .\DesktopFabLauncher
dotnet build
```

## 実行方法 (dotnet)
```powershell
cd .\DesktopFabLauncher
dotnet run
```

## 現状できていること
- 右ボタンを長押しするとポップ(オーバーレイ)が開く。
- 右/左クリックを連続入力すると赤、白の円が順に表示される。
- クリックシーケンス判定: `LLRRR` でエクスプローラを起動する。
- クリックシーケンス判定: `LLRLR` でブラウザを起動する。
