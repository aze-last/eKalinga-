$path = "c:\Users\ASUS\source\repos\eKalinga-\Views\ProjectDistributionPage.xaml"
$code = Get-Content $path -Raw -Encoding UTF8

$target1 = @"
                                  <TextBlock Text="{Binding HouseholdConfirmBeneficiaryName}" FontSize="18" FontWeight="SemiBold" Foreground="{DynamicResource BrandMidnightBrush}" TextWrapping="Wrap"/>
                                  <TextBlock Text="{Binding HouseholdContextSummary}" FontSize="12" Foreground="{DynamicResource BrandTextSecondaryBrush}" TextWrapping="Wrap" Margin="0,2,0,0"/>
"@

$replacement1 = @"
                                  <TextBlock Text="{Binding HouseholdConfirmBeneficiaryName}" FontSize="18" FontWeight="SemiBold" Foreground="{DynamicResource BrandMidnightBrush}" TextWrapping="Wrap"/>
                                  <TextBlock Text="{Binding HouseholdDemographicsSummary}" FontSize="12" Foreground="{DynamicResource BrandTextSecondaryBrush}" TextWrapping="Wrap" Margin="0,2,0,0"/>
                                  <TextBlock Text="{Binding HouseholdContextSummary}" FontSize="12" Foreground="{DynamicResource BrandTextSecondaryBrush}" TextWrapping="Wrap" Margin="0,2,0,0"/>
"@
$code = $code.Replace($target1, $replacement1)

$target2 = @"
                                      <StackPanel>
                                          <TextBlock Text="{Binding HouseholdContextSummary}" FontSize="10" TextWrapping="Wrap"
                                                     Foreground="{DynamicResource BrandTextSecondaryBrush}" Margin="2,0,2,8"/>
"@

$replacement2 = @"
                                      <StackPanel>
                                          <TextBlock Text="{Binding HouseholdDemographicsSummary}" FontSize="10" TextWrapping="Wrap"
                                                     Foreground="{DynamicResource BrandTextSecondaryBrush}" Margin="2,0,2,4"/>
                                          <TextBlock Text="{Binding HouseholdContextSummary}" FontSize="10" TextWrapping="Wrap"
                                                     Foreground="{DynamicResource BrandTextSecondaryBrush}" Margin="2,0,2,8"/>
"@
$code = $code.Replace($target2, $replacement2)

[IO.File]::WriteAllText($path, $code, [Text.Encoding]::UTF8)

