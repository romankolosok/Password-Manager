pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_ENVIRONMENT = 'Development'
        PROJECT_PATH = 'PasswordManager.sln'
        CORE_PROJECT_PATH = 'PasswordManager.Core/PasswordManager.Core.csproj'
        TESTS_PROJECT_PATH = 'PasswordManager.Tests/PasswordManager.Tests.csproj'
        APP_PROJECT_PATH = 'PasswordManager.App/PasswordManager.App.csproj'
        GITHUB_TOKEN = credentials('github-token-id')
        REPO_OWNER = 'romankolosok'
        REPO_NAME = 'Password-Manager'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore & Build') {
            steps {
                sh "dotnet restore ${env.PROJECT_PATH}"
                sh "dotnet build ${env.PROJECT_PATH} --configuration Release --no-restore"
            }
        }

        stage('Test & Coverage') {
            steps {
                sh '''
                    # Integration tests rely on a local Supabase (Docker) stack.
                    # In CI, multiple branch jobs can run on the same agent; Supabase uses global
                    # container names based on project_id, so concurrent starts/stops can interfere.
                    #
                    # We serialize Supabase usage per agent with flock, and fail fast if Supabase
                    # fails to start (rather than running tests with empty env vars).

                    LOCK_FILE="/tmp/passwordmanager-supabase-ci.lock"

                    if command -v flock >/dev/null 2>&1; then
                      flock -w 600 "$LOCK_FILE" sh -eu -c '
                        supabase stop || true
                        trap "supabase stop || true" EXIT

                        supabase start --debug
                        supabase db reset

                        eval "$(supabase status --output env)"
                        [ -n "${API_URL:-}" ] && [ -n "${ANON_KEY:-}" ] && [ -n "${SERVICE_ROLE_KEY:-}" ]

                        export Supabase__Url="$API_URL"
                        export Supabase__AnonKey="$ANON_KEY"
                        export Supabase__ServiceRoleKey="$SERVICE_ROLE_KEY"

                        dotnet test ${TESTS_PROJECT_PATH} \
                            --configuration Release \
                            --no-build \
                            --logger "junit;LogFilePath=test-results.xml" \
                            /p:CollectCoverage=true \
                            /p:CoverletOutputFormat=cobertura \
                            /p:CoverletOutput=coverage.cobertura.xml
                      '
                    else
                      echo "WARN: flock not available; running Supabase without an agent-level lock"
                      supabase stop || true
                      trap "supabase stop || true" EXIT

                      supabase start --debug
                      supabase db reset

                      eval "$(supabase status --output env)"
                      [ -n "${API_URL:-}" ] && [ -n "${ANON_KEY:-}" ] && [ -n "${SERVICE_ROLE_KEY:-}" ]

                      export Supabase__Url="$API_URL"
                      export Supabase__AnonKey="$ANON_KEY"
                      export Supabase__ServiceRoleKey="$SERVICE_ROLE_KEY"

                      dotnet test ${TESTS_PROJECT_PATH} \
                          --configuration Release \
                          --no-build \
                          --logger "junit;LogFilePath=test-results.xml" \
                          /p:CollectCoverage=true \
                          /p:CoverletOutputFormat=cobertura \
                          /p:CoverletOutput=coverage.cobertura.xml
                    fi
                '''
            }
            post {
                always {
                    junit allowEmptyResults: true, testResults: '**/test-results.xml'
                    recordCoverage(
                        tools: [[parser: 'COBERTURA', pattern: '**/coverage.cobertura.xml']],
                        id: 'dotnet-coverage',
                        name: '.NET Coverage'
                    )
                }
            }
        }

        stage('Publish') {
            when {
                allOf {
                    anyOf { branch 'master'; branch 'main' }
                    not { changeRequest() }
                }
            }
            steps {
                sh """
                    dotnet publish ${APP_PROJECT_PATH} \
                        --configuration Release \
                        --self-contained true \
                        -r linux-x64 \
                        -o publish/linux-x64

                    dotnet publish ${APP_PROJECT_PATH} \
                        --configuration Release \
                        --self-contained true \
                        -r win-x64 \
                        -o publish/win-x64
                """

                sh '''
                    cd publish/linux-x64 && tar czf ../../Crypty-linux-x64.tar.gz . && cd ../..
                    cd publish/win-x64  && zip -r ../../Crypty-win-x64.zip .   && cd ../..
                '''
            }
        }

        stage('GitHub Release') {
            when {
                allOf {
                    anyOf { branch 'master'; branch 'main' }
                    not { changeRequest() }
                }
            }
            steps {
                script {
                    def version = sh(
                        script: "sed -n 's/.*<Version>\\([^<]*\\)<\\/Version>.*/\\1/p' ${env.APP_PROJECT_PATH} | tr -d ' \\t\\r'",
                        returnStdout: true
                    ).trim()
                    def tag = version.startsWith('v') ? version : "v${version}"
                    def commitSha = sh(script: 'git rev-parse HEAD', returnStdout: true).trim()
                    def shortSha = commitSha.take(7)

                    sh """
                        curl -sf -X POST \
                            -H "Authorization: token ${GITHUB_TOKEN}" \
                            -H "Content-Type: application/json" \
                            -d '{ \
                                "tag_name": "${tag}", \
                                "target_commitish": "${commitSha}", \
                                "name": "${tag}", \
                                "body": "Automated release ${tag} (${shortSha})\\n\\nPlatforms:\\n- Linux x64\\n- Windows x64\\n\\n**Windows:** If SmartScreen blocks the app, right-click Crypty.exe → Properties → Unblock, then run.", \
                                "draft": false, \
                                "prerelease": false \
                            }' \
                            "https://api.github.com/repos/${REPO_OWNER}/${REPO_NAME}/releases" \
                            -o release.json

                        RELEASE_ID=\$(cat release.json | grep '"id"' | head -1 | sed 's/[^0-9]//g')

                        curl -sf -X POST \
                            -H "Authorization: token ${GITHUB_TOKEN}" \
                            -H "Content-Type: application/gzip" \
                            --data-binary @Crypty-linux-x64.tar.gz \
                            "https://uploads.github.com/repos/${REPO_OWNER}/${REPO_NAME}/releases/\${RELEASE_ID}/assets?name=Crypty-linux-x64.tar.gz"

                        curl -sf -X POST \
                            -H "Authorization: token ${GITHUB_TOKEN}" \
                            -H "Content-Type: application/zip" \
                            --data-binary @Crypty-win-x64.zip \
                            "https://uploads.github.com/repos/${REPO_OWNER}/${REPO_NAME}/releases/\${RELEASE_ID}/assets?name=Crypty-win-x64.zip"
                    """

                    echo "Released ${tag} with Linux and Windows builds"
                }
            }
        }
    }

    post {
        always {
            step([$class: 'GitHubCommitStatusSetter', contextSource: [$class: 'DefaultCommitContextSource']])
            cleanWs()
        }
    }
}
